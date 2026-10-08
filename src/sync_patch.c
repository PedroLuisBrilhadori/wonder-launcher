/*
 * sync_patch.c - Melhoria de sincronizacao de movimento para o cliente Wonder King
 * (Load.exe, build de 2011-05-20, TimeDateStamp 1305856699).
 *
 * Carregado como proxy de dinput8.dll (o Load.exe importa so DirectInput8Create).
 * Instala tres desvios no codigo do jogo:
 *
 *   A) EMISSOR  0x47377a  - heartbeat: enquanto o jogador anda (input horizontal != 0)
 *                           reenvia o pacote de movimento 0x1a a cada N frames.
 *                           O pacote e montado aqui mesmo para NAO passar pelo bloco
 *                           de deteccao de toque duplo (dash) do jogo.
 *   B) RECEPTOR 0x4bf0ea  - quando chega um 0x1a de jogador andando, mede o erro
 *                           entre o X recebido e o X simulado localmente.
 *   C) UPDATE   0x46459f  - a cada frame, aplica esse erro de forma suave no X do
 *                           jogador remoto (ou teleporta, se o erro for grande demais).
 *
 * Tudo e verificado antes de aplicar: se o executavel nao for exatamente o build
 * esperado, nada e alterado e o motivo vai para sync_patch.log.
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdarg.h>
#include <stdint.h>

/* ---------------------------------------------------------------- enderecos do jogo */
#define EXPECTED_TIMESTAMP 1305856699u

#define G_LOCAL_PLAYER   (*(char **)0x00a8859c)          /* ponteiro do personagem local   */
#define G_MY_INDEX       (*(uint16_t *)0x00a86de0)       /* indice do jogador no servidor  */
#define G_FRAME          (*(int *)0x009daf18)            /* contador de frames (60/s)      */
#define G_PVP_MODE       (*(int *)0x0800cdf0)            /* 2 = modo PvP P2P               */
#define G_RESEND_COUNTER (*(int *)0x07c05634)            /* reenvio unico original (600)   */
#define G_PLAYER_TABLE   ((char **)0x009850c0)           /* jogadores por indice           */

typedef int (__cdecl *SendPacket_t)(int sock, int opcode, void *data, int len, int force);
#define GAME_SEND_PACKET ((SendPacket_t)0x00427480)

/* campos do objeto personagem */
#define F_I32(p, off) (*(int *)((p) + (off)))
#define F_F32(p, off) (*(float *)((p) + (off)))
#define OFF_VELX       0x0004
#define OFF_X          0x00bc
#define OFF_Y          0x00c0
#define OFF_STOPFIX    0x00c4   /* 1 = correcao de parada pendente (logica original) */
#define OFF_SENT_X     0x00ec
#define OFF_SENT_Y     0x00f0
#define OFF_INPUT_H    0x00f4   /* -1 / 0 / 1 */
#define OFF_INPUT_V    0x00f8
#define OFF_INPUT_DOWN 0x00fc
#define OFF_DEAD       0x0180
#define OFF_SKILL      0x0188
#define OFF_FLAGS      0xa004
#define OFF_JUMPING    0xa03c
#define OFF_STATE_A040 0xa040
#define OFF_STATE_A048 0xa048   /* knockback */
#define OFF_STATE_A050 0xa050
#define OFF_SPEED      0xb764
#define OFF_B7E8       0xb7e8
#define OFF_F5CC       0xf5cc

/* pontos de desvio e bytes originais esperados */
#define SITE_A      0x0047377a
#define SITE_A_SEND 0x00473793  /* caminho original "enviar" */
#define SITE_A_SKIP 0x00473c75  /* caminho original "nao enviar" */
static const uint8_t ORIG_A[] = {0x81,0x3d,0x34,0x56,0xc0,0x07,0x58,0x02,0x00,0x00,0x74,0x0d,
                                 0x83,0xbe,0xe8,0xb7,0x00,0x00,0x03,0x0f,0x85};
#define SITE_B      0x004bf0ea
#define SITE_B_RET  0x004bf0f4
static const uint8_t ORIG_B[] = {0x0f,0xb7,0x0e,0x8b,0x14,0x8d,0xc0,0x50,0x98,0x00};
#define SITE_C      0x0046459f
#define SITE_C_RET  0x004645a6
static const uint8_t ORIG_C[] = {0x83,0xbe,0xc4,0x00,0x00,0x00,0x01,0x75,0x07};

/* ---------------------------------------------------------------- configuracao */
static struct {
    int heartbeat;        /* liga o reenvio periodico */
    int hb_frames;        /* intervalo em frames (60 = 1 s) */
    int smooth;           /* liga a correcao no receptor */
    int smooth_pct;       /* % do erro corrigido por frame */
    float dead_zone;      /* px: erros menores sao ignorados */
    float snap_dist;      /* px: erros maiores teleportam */
    int log;
    int stats_seconds;
    /* v2 */
    int settle;           /* parado: reenvia a posicao se ela mudou sem tecla (dash, knockback, queda...) */
    int settle_frames;    /* intervalo minimo entre correcoes de parado */
    int settle_dist;      /* px de diferenca para a ultima posicao enviada */
    int ladder_hb;        /* heartbeat tambem subindo/descendo escada/corda */
    int y_fix;            /* receptor: corrige Y do remoto andando */
    int y_fix_dist;       /* px de diferenca no Y para corrigir */
    /* v3 - Battlefield / PvP P2P */
    int pvp_margin;       /* ms somados ao atraso da fila (original: 1 ms) */
    int pvp_redund;       /* liga copias redundantes + descarte de duplicados */
    int pvp_copies;       /* quantos pacotes anteriores vao junto em cada datagrama */
    int pvp_max_age;      /* ms: pacote mais velho que isso nao e mais repetido */
    int pvp_resends;      /* reenvios extras depois do ultimo pacote (jogador parou) */
    int pvp_resend_ms;    /* intervalo entre esses reenvios */
    int pvp_state_frames; /* 0 = off; >0 = manda StateMessage 0x19b a cada N frames andando */
} cfg = {1, 15, 1, 20, 2.0f, 160.0f, 1, 60, 1, 15, 3, 1, 1, 8,
         25, 1, 3, 400, 2, 35, 0};

static char g_dir[MAX_PATH];

/* ---------------------------------------------------------------- log */
static void plog(const char *fmt, ...)
{
    if (!cfg.log) return;
    char path[MAX_PATH], line[512];
    wsprintfA(path, "%ssync_patch.log", g_dir);
    SYSTEMTIME t; GetLocalTime(&t);
    int n = wsprintfA(line, "[%02d:%02d:%02d] ", t.wHour, t.wMinute, t.wSecond);
    va_list ap; va_start(ap, fmt);
    n += wvsprintfA(line + n, fmt, ap);
    va_end(ap);
    line[n++] = '\r'; line[n++] = '\n';
    HANDLE h = CreateFileA(path, FILE_APPEND_DATA, FILE_SHARE_READ, NULL, OPEN_ALWAYS, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return;
    DWORD w; WriteFile(h, line, n, &w, NULL); CloseHandle(h);
}

static int ini_int(const char *key, int def)
{
    char path[MAX_PATH];
    wsprintfA(path, "%ssync_patch.ini", g_dir);
    return (int)GetPrivateProfileIntA("Sync", key, def, path);
}

static void load_config(void)
{
    cfg.heartbeat     = ini_int("Heartbeat", cfg.heartbeat);
    cfg.hb_frames     = ini_int("HeartbeatFrames", cfg.hb_frames);
    cfg.smooth        = ini_int("SmoothCorrection", cfg.smooth);
    cfg.smooth_pct    = ini_int("SmoothPercent", cfg.smooth_pct);
    cfg.dead_zone     = (float)ini_int("DeadZone", (int)cfg.dead_zone);
    cfg.snap_dist     = (float)ini_int("SnapDistance", (int)cfg.snap_dist);
    cfg.log           = ini_int("Log", cfg.log);
    cfg.stats_seconds = ini_int("StatsSeconds", cfg.stats_seconds);
    cfg.settle        = ini_int("IdleCorrection", cfg.settle);
    cfg.settle_frames = ini_int("IdleCorrectionFrames", cfg.settle_frames);
    cfg.settle_dist   = ini_int("IdleCorrectionDistance", cfg.settle_dist);
    cfg.ladder_hb     = ini_int("LadderHeartbeat", cfg.ladder_hb);
    cfg.y_fix         = ini_int("YCorrection", cfg.y_fix);
    cfg.y_fix_dist    = ini_int("YCorrectionDistance", cfg.y_fix_dist);
    cfg.pvp_margin       = ini_int("PvpDelayMargin", cfg.pvp_margin);
    cfg.pvp_redund       = ini_int("PvpRedundancy", cfg.pvp_redund);
    cfg.pvp_copies       = ini_int("PvpCopies", cfg.pvp_copies);
    cfg.pvp_max_age      = ini_int("PvpCopyMaxAge", cfg.pvp_max_age);
    cfg.pvp_resends      = ini_int("PvpResends", cfg.pvp_resends);
    cfg.pvp_resend_ms    = ini_int("PvpResendMs", cfg.pvp_resend_ms);
    cfg.pvp_state_frames = ini_int("PvpStateFrames", cfg.pvp_state_frames);
    if (cfg.pvp_margin < 1) cfg.pvp_margin = 1;
    if (cfg.pvp_margin > 150) cfg.pvp_margin = 150;
    if (cfg.pvp_copies < 1) cfg.pvp_copies = 1;
    if (cfg.pvp_copies > 6) cfg.pvp_copies = 6;
    if (cfg.pvp_resends < 0) cfg.pvp_resends = 0;
    if (cfg.pvp_resends > 4) cfg.pvp_resends = 4;
    if (cfg.pvp_resend_ms < 10) cfg.pvp_resend_ms = 10;
    if (cfg.pvp_state_frames != 0 && cfg.pvp_state_frames < 10) cfg.pvp_state_frames = 10;
    if (cfg.hb_frames < 2) cfg.hb_frames = 2;
    if (cfg.settle_frames < 6) cfg.settle_frames = 6;
    if (cfg.settle_dist < 1) cfg.settle_dist = 1;
    if (cfg.y_fix_dist < 2) cfg.y_fix_dist = 2;
    if (cfg.smooth_pct < 1) cfg.smooth_pct = 1;
    if (cfg.smooth_pct > 100) cfg.smooth_pct = 100;
}

/* ---------------------------------------------------------------- estatisticas (dados p/ o TCC) */
static struct {
    unsigned hb_sent, rx_moving, corrections, snaps;
    unsigned settle_sent, y_fixes;
    unsigned pvp_sent, pvp_resent, pvp_recovered, pvp_dups, pvp_states;
    float err_sum, err_max;
    int last_report_frame;
} st;

static int iabs(int v) { return v < 0 ? -v : v; }
static float fabsf_(float v) { return v < 0 ? -v : v; }

static void maybe_report(void)
{
    if (cfg.stats_seconds <= 0) return;
    int f = G_FRAME;
    if (st.last_report_frame == 0) { st.last_report_frame = f; return; }
    if (f - st.last_report_frame < cfg.stats_seconds * 60 && f >= st.last_report_frame) return;
    int avg10 = st.rx_moving ? (int)(st.err_sum * 10.0f / (float)st.rx_moving) : 0;
    plog("stats: heartbeats=%u correcoes_parado=%u recebidos_andando=%u correcoes=%u teleportes=%u correcoes_y=%u erro_medio=%d.%dpx erro_max=%dpx",
         st.hb_sent, st.settle_sent, st.rx_moving, st.corrections, st.snaps, st.y_fixes,
         avg10 / 10, avg10 % 10, (int)st.err_max);
    if (st.pvp_sent || st.pvp_recovered || st.pvp_dups)
        plog("stats pvp: enviados=%u reenvios=%u recuperados=%u duplicados_descartados=%u state_periodico=%u",
             st.pvp_sent, st.pvp_resent, st.pvp_recovered, st.pvp_dups, st.pvp_states);
    st.pvp_sent = st.pvp_resent = st.pvp_recovered = st.pvp_dups = st.pvp_states = 0;
    st.hb_sent = st.rx_moving = st.corrections = st.snaps = 0;
    st.settle_sent = st.y_fixes = 0;
    st.err_sum = st.err_max = 0;
    st.last_report_frame = f;
}

/* ---------------------------------------------------------------- A) emissor */
static int g_last_gate_frame = -100;
static int g_last_hb_frame;

static void send_move_packet(char *p)
{
    /* mesmo layout que o jogo monta em 0x473ba2..0x473c67 */
    uint8_t buf[16];
    int x = (int)F_F32(p, OFF_X);
    int y = 0;
    F_I32(p, OFF_SENT_X) = x;
    if (F_I32(p, OFF_JUMPING) == 0) y = (int)F_F32(p, OFF_Y);
    F_I32(p, OFF_SENT_Y) = y;

    *(uint16_t *)(buf + 0) = G_MY_INDEX;
    buf[2] = (uint8_t)F_I32(p, OFF_INPUT_H);
    buf[3] = (uint8_t)F_I32(p, OFF_INPUT_DOWN);
    *(uint16_t *)(buf + 4) = (uint16_t)x;
    *(uint16_t *)(buf + 6) = (uint16_t)y;
    *(uint32_t *)(buf + 8) = (uint32_t)F_I32(p, OFF_FLAGS);
    buf[12] = F_I32(p, OFF_F5CC) ? (uint8_t)F_I32(p, OFF_INPUT_V) : 0;
    GAME_SEND_PACKET(1, 0x1a, buf, 13, 0);
}

/* sem estados especiais (skill, golpe recebido, knockback...) e fora do PvP P2P */
static int free_state(char *p)
{
    return F_I32(p, OFF_DEAD) == 0
        && F_I32(p, OFF_SKILL) == 0
        && F_I32(p, OFF_STATE_A040) == 0
        && F_I32(p, OFF_STATE_A048) == 0
        && F_I32(p, OFF_STATE_A050) == 0
        && G_PVP_MODE != 2;
}

static int on_ladder_moving(char *p)
{
    return cfg.ladder_hb && F_I32(p, OFF_F5CC) != 0 && F_I32(p, OFF_INPUT_V) != 0;
}

static int can_heartbeat(char *p)
{
    return (F_I32(p, OFF_INPUT_H) != 0 || on_ladder_moving(p)) && free_state(p);
}

/* v2: parado no chao, mas a posicao real se afastou da ultima enviada (dash, knockback,
   skill que desloca, queda de plataforma, pouso de pulo sem tecla). O jogo original so
   reenviaria depois de 10 s (contador 600). Manda um 0x1a de "parado" com a posicao certa;
   quem recebe usa a mesma correcao de parada de quando alguem solta a tecla. */
static int g_last_settle_frame = -1000;

static int want_settle(char *p, int f)
{
    if (!cfg.settle) return 0;
    if (F_I32(p, OFF_INPUT_H) != 0 || F_I32(p, OFF_INPUT_V) != 0) return 0;
    if (F_I32(p, OFF_JUMPING) != 0 || !free_state(p)) return 0;
    if (iabs(f - g_last_settle_frame) < cfg.settle_frames) return 0;
    int x = (int)F_F32(p, OFF_X), y = (int)F_F32(p, OFF_Y);
    return iabs(x - F_I32(p, OFF_SENT_X)) >= cfg.settle_dist
        || iabs(y - F_I32(p, OFF_SENT_Y)) >= cfg.settle_dist;
}

/* Chamado no lugar do teste original. Retorna 1 = seguir o caminho original de envio. */
int __cdecl gate_decide(char *p)
{
    /* condicoes originais do jogo, intactas */
    if (G_RESEND_COUNTER == 600) return 1;
    if (F_I32(p, OFF_B7E8) == 3) return 1;

    int f = G_FRAME;
    /* o teste so roda quando o input NAO mudou; se houve buraco na sequencia,
       o jogo acabou de mandar um 0x1a normal -> reinicia o intervalo */
    if (f != g_last_gate_frame + 1) g_last_hb_frame = f;
    g_last_gate_frame = f;

    if (cfg.heartbeat && can_heartbeat(p) && iabs(f - g_last_hb_frame) >= cfg.hb_frames) {
        send_move_packet(p);
        g_last_hb_frame = f;
        st.hb_sent++;
    } else if (want_settle(p, f)) {
        send_move_packet(p);          /* input_h = 0 -> os outros tratam como "parou aqui" */
        g_last_settle_frame = f;
        st.settle_sent++;
    }
    maybe_report();
    return 0;
}

volatile int g_gate_result;  /* usado pelo stub em assembly */

__attribute__((naked)) void stub_gate(void)
{
    __asm__ volatile(
        ".intel_syntax noprefix\n"
        "pushad\n"
        "push esi\n"
        "call _gate_decide\n"
        "add esp, 4\n"
        "mov dword ptr [_g_gate_result], eax\n"
        "popad\n"
        "cmp dword ptr [_g_gate_result], 0\n"
        "je 1f\n"
        "push 0x00473793\n"
        "ret\n"
        "1:\n"
        "push 0x00473c75\n"
        "ret\n"
        ".att_syntax\n");
}

/* ---------------------------------------------------------------- B/C) receptor */
typedef struct { char *p; float pending; } Corr;
#define CORR_SLOTS 1024
static Corr g_corr[CORR_SLOTS];

static Corr *corr_slot(char *p, int create)
{
    unsigned h = ((uintptr_t)p >> 4) & (CORR_SLOTS - 1);
    for (int i = 0; i < CORR_SLOTS; i++) {
        Corr *c = &g_corr[(h + i) & (CORR_SLOTS - 1)];
        if (c->p == p) return c;
        if (c->p == NULL) {
            if (!create) return NULL;
            c->p = p; c->pending = 0; return c;
        }
    }
    return NULL;
}

/* cmd = objeto do comando 0x1a recebido: +0x10 idx, +0x12 inputH, +0x14 X, +0x16 Y */
void __cdecl on_remote_move(char *cmd)
{
    if (!cfg.smooth) return;
    uint16_t idx = *(uint16_t *)(cmd + 0x10);
    char *p = G_PLAYER_TABLE[idx];
    if (!p || p == G_LOCAL_PLAYER) return;

    float target = (float)*(uint16_t *)(cmd + 0x14) + 0.5f;  /* X chega truncado */
    float err = target - F_F32(p, OFF_X);
    float aerr = fabsf_(err);
    st.rx_moving++;
    st.err_sum += aerr;
    if (aerr > st.err_max) st.err_max = aerr;

    /* v2: Y. O jogo original so aplica o Y recebido quando o jogador PARA. Se o remoto
       subiu/desceu de plataforma de um jeito diferente aqui, ele anda "no andar errado"
       ate parar. Y = 0 significa "no ar" e e ignorado; remoto pulando tambem. */
    uint16_t ry = *(uint16_t *)(cmd + 0x16);
    if (cfg.y_fix && ry && F_I32(p, OFF_JUMPING) == 0) {
        float ty = (float)ry - 1.0f;               /* mesmo ajuste da correcao de parada */
        if (fabsf_(ty - F_F32(p, OFF_Y)) >= (float)cfg.y_fix_dist) {
            F_F32(p, OFF_Y) = ty;
            st.y_fixes++;
        }
    }

    Corr *c = corr_slot(p, 1);
    if (!c) return;
    if (aerr <= cfg.dead_zone) { c->pending = 0; return; }
    if (aerr >= cfg.snap_dist) {
        F_F32(p, OFF_X) = target;
        uint16_t y = *(uint16_t *)(cmd + 0x16);
        if (y) F_F32(p, OFF_Y) = (float)y - 1.0f;  /* mesmo ajuste que o jogo faz na parada */
        c->pending = 0;
        st.snaps++;
        return;
    }
    c->pending = err;
    st.corrections++;
}

void __cdecl on_char_tick(char *p)
{
    if (!cfg.smooth || p == G_LOCAL_PLAYER) return;
    Corr *c = corr_slot(p, 0);
    if (!c || c->pending == 0) return;
    /* parou: a correcao de parada original assume */
    if (F_I32(p, OFF_STOPFIX) == 1 || F_I32(p, OFF_INPUT_H) == 0) { c->pending = 0; return; }

    float step = c->pending * (float)cfg.smooth_pct / 100.0f;
    float max_step = F_F32(p, OFF_SPEED) * 0.5f;
    if (max_step < 1.0f) max_step = 1.0f;
    if (step > max_step) step = max_step;
    if (step < -max_step) step = -max_step;
    if (fabsf_(c->pending - step) < 0.25f) step = c->pending;
    F_F32(p, OFF_X) += step;
    c->pending -= step;
}

__attribute__((naked)) void stub_remote_move(void)
{
    __asm__ volatile(
        ".intel_syntax noprefix\n"
        "pushad\n"
        "push edi\n"
        "call _on_remote_move\n"
        "add esp, 4\n"
        "popad\n"
        "movzx ecx, word ptr [esi]\n"
        "mov edx, dword ptr [ecx*4 + 0x009850c0]\n"
        "push 0x004bf0f4\n"
        "ret\n"
        ".att_syntax\n");
}

__attribute__((naked)) void stub_char_tick(void)
{
    __asm__ volatile(
        ".intel_syntax noprefix\n"
        "pushad\n"
        "push esi\n"
        "call _on_char_tick\n"
        "add esp, 4\n"
        "popad\n"
        "cmp dword ptr [esi + 0xc4], 1\n"
        "push 0x004645a6\n"
        "ret\n"
        ".att_syntax\n");
}

/* ---------------------------------------------------------------- D) Battlefield / PvP P2P (v3)
 *
 * No PvP o jogo troca pacotes UDP direto entre os jogadores ([u16 len][u16 op][u32 frame]
 * + dados + double timestamp) e executa tudo por uma fila ordenada por timestamp com um
 * atraso fixo (lockstep por atraso). UDP sem nenhuma confirmacao: pacote perdido = acao
 * perdida (golpe, skill, movimento). Aqui:
 *   - cada datagrama de jogo leva junto copias dos ultimos pacotes enviados;
 *   - quando o jogador para de mandar, os ultimos pacotes sao repetidos algumas vezes;
 *   - o receptor descarta o que ja recebeu (pelo conteudo inteiro, que inclui o timestamp).
 * As copias vao DEPOIS do pacote principal ou num datagrama com cabecalho de tamanho 0:
 * cliente sem o patch le so o primeiro pacote e ignora o resto, entao nada e aplicado
 * duas vezes nele (ele so nao ganha a recuperacao).
 */
#define G_UDP_BUF   (*(uint8_t **)0x00915d30)
#define G_PAYLOAD   (*(uint8_t **)0x00990d2c)
#define G_FROM      ((uint8_t *)0x07994df8)
#define G_FROMLEN   ((int *)0x07993de0)
#define G_HDR0      (*(uint32_t *)0x07993e1c)
#define G_HDR1      (*(uint32_t *)0x07993e20)

typedef int  (__stdcall *RecvFrom_t)(uintptr_t s, void *buf, int len, int flags, void *from, int *fromlen);
typedef void (__cdecl *Dispatch_t)(int op, int len);
typedef void (__fastcall *SendAll_t)(void *mgr, void *edx, void *buf, int len);
typedef void (__cdecl *Fn1_t)(char *p);
typedef void (__cdecl *StateMsg_t)(char *p, int revision_flag);
#define GAME_RECVFROM  ((RecvFrom_t)0x0081ea76)
#define GAME_DISPATCH  ((Dispatch_t)0x00418ae0)
#define GAME_SENDALL   ((SendAll_t)0x007d9c90)
#define GAME_PVP_INPUT ((Fn1_t)0x007d67c0)
#define GAME_STATE_MSG ((StateMsg_t)0x007d5d00)

#define CALL_RECV   0x00404d6c   /* call FUN_004268c0 (le o socket UDP)         */
#define CALL_SEND   0x007da08e   /* call FUN_007d9c90 (manda p/ todos os peers) */
#define CALL_INPUT  0x00473f49   /* call FUN_007d67c0 (input local no PvP)      */
#define FADD_MARGIN_1 0x007d2caf /* fadd [0x859038] = +1.0 ms no atraso         */
#define FADD_MARGIN_2 0x007d2bbb
static const uint8_t ORIG_FADD[] = {0xdc, 0x05, 0x38, 0x90, 0x85, 0x00};

#define BLOCK_MAGIC 0xC55Au
#define PKT_MAX     308           /* o jogo recusa payload > 300 */

static int is_game_op(unsigned op) { return op >= 0x198 && op <= 0x1a3; }

static void bcopy_(uint8_t *d, const uint8_t *s, int n) { while (n-- > 0) *d++ = *s++; }

static double now_ms(void)
{
    static LARGE_INTEGER f;
    LARGE_INTEGER c;
    if (!f.QuadPart) QueryPerformanceFrequency(&f);
    QueryPerformanceCounter(&c);
    return (double)c.QuadPart * 1000.0 / (double)f.QuadPart;
}

/* ----- emissor: ultimos pacotes enviados */
#define RING 8
static struct { uint8_t b[PKT_MAX]; int len; double t; } g_ring[RING];
static int g_ring_n, g_ring_head;      /* head = proxima posicao */
static void *g_mgr;
static double g_last_send_t;
static int g_resends_done;
static int g_last_state_frame = -1000;

static void ring_clear(void) { g_ring_n = 0; g_ring_head = 0; g_mgr = NULL; g_resends_done = 0; }

static void ring_push(const uint8_t *p, int len, double t)
{
    bcopy_(g_ring[g_ring_head].b, p, len);
    g_ring[g_ring_head].len = len;
    g_ring[g_ring_head].t = t;
    g_ring_head = (g_ring_head + 1) % RING;
    if (g_ring_n < RING) g_ring_n++;
}

/* escreve [u16 magic][u8 n][u8 0] + ate cfg.pvp_copies pacotes recentes (mais velho primeiro) */
static int write_block(uint8_t *d, double t)
{
    int idx[RING], n = 0;
    for (int i = 1; i <= g_ring_n && n < cfg.pvp_copies; i++) {
        int k = (g_ring_head - i + RING) % RING;
        if (t - g_ring[k].t > (double)cfg.pvp_max_age) break;
        idx[n++] = k;
    }
    if (!n) return 0;
    *(uint16_t *)d = BLOCK_MAGIC; d[2] = (uint8_t)n; d[3] = 0;
    int o = 4;
    for (int i = n - 1; i >= 0; i--) {
        bcopy_(d + o, g_ring[idx[i]].b, g_ring[idx[i]].len);
        o += g_ring[idx[i]].len;
    }
    return o;
}

void __fastcall hook_p2p_send(void *mgr, void *edx, uint8_t *pkt, int len)
{
    unsigned op = *(uint16_t *)(pkt + 2);
    if (!cfg.pvp_redund || G_PVP_MODE != 2 || !is_game_op(op) || len < 16 || len > PKT_MAX) {
        GAME_SENDALL(mgr, edx, pkt, len);
        return;
    }
    if (mgr != g_mgr) ring_clear();
    g_mgr = mgr;
    uint8_t d[PKT_MAX * (RING + 1) + 8];
    double t = now_ms();
    bcopy_(d, pkt, len);
    int n = len + write_block(d + len, t);
    GAME_SENDALL(mgr, edx, d, n);
    ring_push(pkt, len, t);
    g_last_send_t = t;
    g_resends_done = 0;
    if (op == 0x19b) g_last_state_frame = G_FRAME;
    st.pvp_sent++;
}

/* jogador parou de mandar: repete os ultimos pacotes num datagrama que cliente sem patch ignora */
static void pvp_resend_tick(void)
{
    if (!cfg.pvp_redund || !g_mgr || !g_ring_n || g_resends_done >= cfg.pvp_resends) return;
    double t = now_ms();
    if (t - g_last_send_t < (double)cfg.pvp_resend_ms * (g_resends_done + 1)) return;
    uint8_t d[PKT_MAX * RING + 8];
    *(uint32_t *)d = 0;                       /* len 0 -> descartado pelo jogo original */
    int n = write_block(d + 2, t);
    g_resends_done++;
    if (!n) { g_resends_done = cfg.pvp_resends; return; }
    GAME_SENDALL(g_mgr, NULL, d, n + 2);
    st.pvp_resent++;
}

/* ----- receptor: descarte de duplicados */
#define SEEN 1024
static uint64_t g_seen[SEEN];
static int g_seen_pos;

static int seen_before(const uint8_t *pkt, int len)
{
    uint64_t h = 1469598103934665603ull;
    for (int i = 0; i < 8; i++) { h ^= G_FROM[i]; h *= 1099511628211ull; }   /* porta + ip */
    for (int i = 0; i < len; i++) { h ^= pkt[i]; h *= 1099511628211ull; }
    if (!h) h = 1;
    for (int i = 0; i < SEEN; i++) if (g_seen[i] == h) return 1;
    g_seen[g_seen_pos] = h;
    g_seen_pos = (g_seen_pos + 1) % SEEN;
    return 0;
}

static void dispatch_one(const uint8_t *pkt, int len, int from_block)
{
    unsigned op = *(uint16_t *)(pkt + 2);
    if (len < 8) return;
    if (is_game_op(op) && len >= 16) {
        if (seen_before(pkt, len)) { st.pvp_dups++; return; }
        if (from_block) st.pvp_recovered++;
    }
    G_HDR0 = *(const uint32_t *)pkt;
    G_HDR1 = *(const uint32_t *)(pkt + 4);
    *(uint32_t *)G_PAYLOAD = 0;
    bcopy_(G_PAYLOAD, pkt + 8, len - 8);
    GAME_DISPATCH((int)op, len - 8);
}

static void dispatch_block(const uint8_t *b, int avail)
{
    if (avail < 4 || *(const uint16_t *)b != BLOCK_MAGIC) return;
    int n = b[2], o = 4;
    for (int i = 0; i < n; i++) {
        if (avail - o < 16) return;
        int len = *(const uint16_t *)(b + o);
        if (len < 16 || len > PKT_MAX || len > avail - o) return;
        if (!is_game_op(*(const uint16_t *)(b + o + 2))) return;
        dispatch_one(b + o, len, 1);
        o += len;
    }
}

/* substitui FUN_004268c0 inteira (mesma leitura; depois trata as copias) */
int __cdecl hook_udp_recv(uintptr_t sock, int unused)
{
    (void)unused;
    for (int i = 0; i < 16; i++) G_FROM[i] = 0;
    uint8_t *buf = G_UDP_BUF;
    *(uint32_t *)buf = 0;
    int n = GAME_RECVFROM(sock, buf, 1000000, 0, G_FROM, G_FROMLEN);
    if (n == -1 || n == 0) return 0;
    int len = n >= 2 ? *(uint16_t *)buf : 0;
    if (len < 8) {
        if (cfg.pvp_redund && n >= 6 && len == 0) dispatch_block(buf + 2, n - 2);
        return 0;
    }
    if (cfg.pvp_redund && n > len) dispatch_block(buf + len, n - len);   /* copias antes (mais velhas) */
    dispatch_one(buf, len, 0);
    return 0;
}

/* no lugar do call FUN_007d67c0 (input local, so roda em PvP) */
void __cdecl hook_pvp_input(char *p)
{
    GAME_PVP_INPUT(p);
    if (G_PVP_MODE != 2) { if (g_ring_n || g_mgr) ring_clear(); return; }
    pvp_resend_tick();
    if (cfg.pvp_state_frames > 0 && p == G_LOCAL_PLAYER && F_I32(p, OFF_INPUT_H) != 0
        && F_I32(p, OFF_DEAD) == 0 && F_I32(p, OFF_SKILL) == 0 && F_I32(p, OFF_STATE_A040) == 0
        && F_I32(p, OFF_STATE_A048) == 0 && F_I32(p, OFF_STATE_A050) == 0
        && iabs(G_FRAME - g_last_state_frame) >= cfg.pvp_state_frames) {
        g_last_state_frame = G_FRAME;
        GAME_STATE_MSG(p, 1);
        st.pvp_states++;
    }
    maybe_report();
}

static double g_margin_ms;

/* ---------------------------------------------------------------- instalacao */
static int bytes_match(uintptr_t addr, const uint8_t *orig, size_t n)
{
    for (size_t i = 0; i < n; i++)
        if (((const uint8_t *)addr)[i] != orig[i]) return 0;
    return 1;
}

static void write_jmp(uintptr_t at, void *to, size_t total)
{
    DWORD old;
    VirtualProtect((void *)at, total, PAGE_EXECUTE_READWRITE, &old);
    uint8_t *b = (uint8_t *)at;
    b[0] = 0xe9;
    *(int32_t *)(b + 1) = (int32_t)((uintptr_t)to - (at + 5));
    for (size_t i = 5; i < total; i++) b[i] = 0x90;
    VirtualProtect((void *)at, total, old, &old);
    FlushInstructionCache(GetCurrentProcess(), (void *)at, total);
}

static int call_targets(uintptr_t at, uintptr_t target)
{
    const uint8_t *b = (const uint8_t *)at;
    return b[0] == 0xe8 && at + 5 + *(const int32_t *)(b + 1) == target;
}

static void set_call(uintptr_t at, void *to)
{
    DWORD old;
    VirtualProtect((void *)at, 5, PAGE_EXECUTE_READWRITE, &old);
    *(int32_t *)(at + 1) = (int32_t)((uintptr_t)to - (at + 5));
    VirtualProtect((void *)at, 5, old, &old);
    FlushInstructionCache(GetCurrentProcess(), (void *)at, 5);
}

static void set_fadd_operand(uintptr_t at, double *d)
{
    DWORD old;
    VirtualProtect((void *)at, 6, PAGE_EXECUTE_READWRITE, &old);
    *(uint32_t *)(at + 2) = (uint32_t)(uintptr_t)d;
    VirtualProtect((void *)at, 6, old, &old);
    FlushInstructionCache(GetCurrentProcess(), (void *)at, 6);
}

static void install_pvp(void)
{
    int okR = call_targets(CALL_RECV, 0x004268c0);
    int okS = call_targets(CALL_SEND, 0x007d9c90);
    int okI = call_targets(CALL_INPUT, 0x007d67c0);
    int okM = bytes_match(FADD_MARGIN_1, ORIG_FADD, sizeof ORIG_FADD)
           && bytes_match(FADD_MARGIN_2, ORIG_FADD, sizeof ORIG_FADD);
    if (!okR || !okS || !okI || !okM) {
        plog("pvp: bytes originais diferentes (R=%d S=%d I=%d M=%d) - parte PvP NAO aplicada", okR, okS, okI, okM);
        return;
    }
    set_call(CALL_RECV, hook_udp_recv);
    set_call(CALL_SEND, hook_p2p_send);
    set_call(CALL_INPUT, hook_pvp_input);
    if (cfg.pvp_margin != 1) {
        g_margin_ms = (double)cfg.pvp_margin;
        set_fadd_operand(FADD_MARGIN_1, &g_margin_ms);
        set_fadd_operand(FADD_MARGIN_2, &g_margin_ms);
    }
    plog("pvp v3: margem de atraso=%d ms, redundancia=%d (%d copias, ate %d ms), reenvios=%d a cada %d ms, state periodico=%d frames",
         cfg.pvp_margin, cfg.pvp_redund, cfg.pvp_copies, cfg.pvp_max_age, cfg.pvp_resends,
         cfg.pvp_resend_ms, cfg.pvp_state_frames);
}

static void install(void)
{
    HMODULE exe = GetModuleHandleA(NULL);
    IMAGE_DOS_HEADER *dos = (IMAGE_DOS_HEADER *)exe;
    IMAGE_NT_HEADERS *nt = (IMAGE_NT_HEADERS *)((char *)exe + dos->e_lfanew);
    if ((uintptr_t)exe != 0x400000 || nt->FileHeader.TimeDateStamp != EXPECTED_TIMESTAMP) {
        plog("executavel nao reconhecido (base %p, timestamp %lu) - patch NAO aplicado",
             exe, (unsigned long)nt->FileHeader.TimeDateStamp);
        return;
    }
    install_pvp();
    int okA = bytes_match(SITE_A, ORIG_A, sizeof ORIG_A);
    int okB = bytes_match(SITE_B, ORIG_B, sizeof ORIG_B);
    int okC = bytes_match(SITE_C, ORIG_C, sizeof ORIG_C);
    if (!okA || !okB || !okC) {
        plog("bytes originais diferentes (A=%d B=%d C=%d) - patch NAO aplicado", okA, okB, okC);
        return;
    }
    write_jmp(SITE_A, stub_gate, 19);        /* cmp/je/cmp; o jne seguinte vira codigo morto */
    write_jmp(SITE_B, stub_remote_move, 10);
    write_jmp(SITE_C, stub_char_tick, 7);    /* o jne em 0x4645a6 continua valendo */
    plog("patch v3 aplicado: heartbeat=%d a cada %d frames (%d ms), escada=%d, suavizacao=%d (%d%%/frame, zona morta %dpx, teleporte >= %dpx)",
         cfg.heartbeat, cfg.hb_frames, cfg.hb_frames * 1000 / 60, cfg.ladder_hb, cfg.smooth, cfg.smooth_pct,
         (int)cfg.dead_zone, (int)cfg.snap_dist);
    plog("  correcao parado=%d (>= %dpx, a cada %d frames), correcao Y=%d (>= %dpx)",
         cfg.settle, cfg.settle_dist, cfg.settle_frames, cfg.y_fix, cfg.y_fix_dist);
}

/* ---------------------------------------------------------------- proxy dinput8 */
typedef HRESULT (WINAPI *DI8Create_t)(HINSTANCE, DWORD, const void *, void **, void *);
static DI8Create_t real_create;

/* exporta sem a decoracao stdcall (_DirectInput8Create@20), como a DLL original */
__asm__(".section .drectve\n"
        ".ascii \" -export:DirectInput8Create=DirectInput8Create@20\"\n"
        ".text\n");

HRESULT WINAPI DirectInput8Create(HINSTANCE inst, DWORD ver, const void *riid, void **out, void *outer)
{
    if (!real_create) {
        char path[MAX_PATH];
        UINT n = GetSystemDirectoryA(path, MAX_PATH);
        lstrcpyA(path + n, "\\dinput8.dll");
        HMODULE m = LoadLibraryA(path);
        if (m) real_create = (DI8Create_t)GetProcAddress(m, "DirectInput8Create");
        if (!real_create) { plog("falha ao carregar %s", path); return E_FAIL; }
    }
    return real_create(inst, ver, riid, out, outer);
}

BOOL WINAPI DllMain(HINSTANCE self, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(self);
        GetModuleFileNameA(self, g_dir, MAX_PATH);
        char *s = g_dir + lstrlenA(g_dir);
        while (s > g_dir && s[-1] != '\\') s--;
        *s = 0;
        load_config();
        install();
    }
    return TRUE;
}
