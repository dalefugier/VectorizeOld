using Rhino.ApplicationSettings;
using Rhino.Display;
using System.Collections.Generic;
using System.Drawing;

namespace Vectorize
{
  public class VectorizeConduit : DisplayConduit
  {
    public VectorizeConduit(Color color)
    {
      ListOfPathes = new List<List<Curve>>();
      DrawColor = color;
    }

    public List<List<Curve>> ListOfPathes { get; set; }

    public Color DrawColor { get; private set; }

    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    {
      var bbox = new Rhino.Geometry.BoundingBox();
      for (var i = 0; i < ListOfPathes.Count; i++)
      {
        var list = ListOfPathes[i];
        for (var j = 0; j < list.Count; j++)
          bbox.Union(list[j].BoundingBox());
      }
      e.IncludeBoundingBox(bbox);
    }

    protected override void DrawOverlay(DrawEventArgs e)
    {
      for (var i = 0; i < ListOfPathes.Count; i++)
      {
        var list = ListOfPathes[i];
        for (var j = 0; j < list.Count; j++)
        {
          var curve = list[j].ToCurve();
          if (null != curve)
            e.Display.DrawCurve(curve, DrawColor);
        }
      }
    }
  }
}
