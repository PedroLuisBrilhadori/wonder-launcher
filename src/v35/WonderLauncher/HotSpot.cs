using System.Drawing;

namespace WonderLauncher;

internal class HotSpot
{
	public RectangleF Rect;

	public float Hover;

	public bool Over;

	public bool Down;

	public HotSpot(float x, float y, float w, float h)
	{
		Rect = new RectangleF(x, y, w, h);
	}
}
