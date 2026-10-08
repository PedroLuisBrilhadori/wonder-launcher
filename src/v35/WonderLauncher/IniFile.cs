using System.Runtime.InteropServices;
using System.Text;

namespace WonderLauncher;

public class IniFile
{
	private readonly string path;

	[DllImport("kernel32", CharSet = CharSet.Unicode)]
	private static extern bool WritePrivateProfileString(string section, string key, string val, string filePath);

	[DllImport("kernel32", CharSet = CharSet.Unicode)]
	private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

	public IniFile(string iniPath)
	{
		path = iniPath;
	}

	public void Write(string section, string key, string value)
	{
		WritePrivateProfileString(section, key, value, path);
	}

	public string Read(string section, string key, string defaultVal)
	{
		StringBuilder stringBuilder = new StringBuilder(255);
		GetPrivateProfileString(section, key, defaultVal, stringBuilder, 255, path);
		return stringBuilder.ToString();
	}
}
