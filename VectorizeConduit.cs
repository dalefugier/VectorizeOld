using Rhino.Display;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Drawing;

namespace Vectorize
{
  /// <summary>
  /// Vectorize display conduit.
  /// </summary>
  public class VectorizeConduit : DisplayConduit
  {
    private Color m_color;
    private BoundingBox m_bbox;

    /// <summary>
    /// Public constructor
    /// </summary>
    /// <param name="color">The color to use when drawing preview curves.</param>
    public VectorizeConduit(Color color)
    {
      m_color = color;
      m_bbox = BoundingBox.Unset;
      CurvePaths = new List<List<Vectorize.Curve>>();
      OutlineCurves = new List<PolyCurve>();
    }

    /// <summary>
    /// The list of path curves, as computed by Potrace
    /// </summary>
    public List<List<Vectorize.Curve>> CurvePaths
    {
      get;
      private set;
    }

    /// <summary>
    /// The list of outline curves created from the path curves.
    /// These curve may end up in the Rhino document.
    /// </summary>
    public List<PolyCurve> OutlineCurves
    {
      get;
      private set;
    }

    /// <summary>
    /// Clears collections and resets the conduit bounding box.
    /// </summary>
    public void Clear()
    {
      m_bbox = BoundingBox.Unset;
      CurvePaths.Clear();
      OutlineCurves.Clear();
    }

    /// <summary>
    /// DisplayConduit.CalculateBoundingBox override
    /// </summary>
    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    {
      if (!m_bbox.IsValid)
        CreateOutlineCurves();
      if (m_bbox.IsValid)
        e.IncludeBoundingBox(m_bbox);
    }

    /// <summary>
    /// DisplayConduit.DrawOverlay override
    /// </summary>
    protected override void DrawOverlay(DrawEventArgs e)
    {
      for (var i = 0; i < OutlineCurves.Count; i++)
        e.Display.DrawCurve(OutlineCurves[i], m_color);
    }

    /// <summary>
    /// Creates outline curves from the path curves computed by Potrace.
    /// Also cooks up the conduit bounding box.
    /// </summary>
    protected int CreateOutlineCurves()
    {
      OutlineCurves.Clear();

      for (var i = 0; i < CurvePaths.Count; i++)
      {
        var curve_path = CurvePaths[i];
        if (0 == curve_path.Count)
          continue;

        var point1 = curve_path[0].A.ToPoint3d();
        var point2 = curve_path[0].A.ToPoint3d();

        var polycurve = new PolyCurve();

        for (var j = 0; j < curve_path.Count; j++)
        {
          var curve = curve_path[j];
          if (curve.Kind == CurveKind.Line)
          {
            if (j > 0)
              polycurve.Append(new LineCurve(point1, curve.A.ToPoint3d()));
            polycurve.Append(curve.ToLineCurve());
          }
          else
          {
            polycurve.Append(curve.ToNurbsCurve());
          }
          point1 = curve.B.ToPoint3d();
        }
        
        if (!polycurve.MakeClosed(0.01))
        {
          polycurve.Append(new LineCurve(point1, point2));
        }

        OutlineCurves.Add(polycurve);
      }

      if (OutlineCurves.Count > 0)
      {
        m_bbox = new BoundingBox();
        for (var i = 0; i < OutlineCurves.Count; i++)
          m_bbox.Union(OutlineCurves[i].GetBoundingBox(true));
      }

      return OutlineCurves.Count;
    }
  }
}
