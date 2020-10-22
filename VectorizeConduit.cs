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
    private readonly List<List<Vectorize.Curve>> m_path_curves;
    private readonly Bitmap m_bitmap;
    private readonly double m_scale;
    private readonly double m_tolerance;
    private readonly Color m_color;
    private BoundingBox m_bbox;

    /// <summary>
    /// Public constructor.
    /// </summary>
    public VectorizeConduit(Bitmap bitmap, double scale, double tolerance, Color color)
    {
      m_path_curves = new List<List<Vectorize.Curve>>();
      m_bitmap = bitmap;
      m_scale = scale;
      m_tolerance = tolerance;
      m_color = color;
      m_bbox = BoundingBox.Unset;
      OutlineCurves = new List<PolyCurve>();
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
    /// DisplayConduit.CalculateBoundingBox override.
    /// </summary>
    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    {
      if (!m_bbox.IsValid)
        CreateOutlineCurves();
      if (m_bbox.IsValid)
        e.IncludeBoundingBox(m_bbox);
    }

    /// <summary>
    /// DisplayConduit.CalculateBoundingBoxZoomExtents override.
    /// </summary>
    protected override void CalculateBoundingBoxZoomExtents(CalculateBoundingBoxEventArgs e)
    {
      CalculateBoundingBox(e);
    }

    /// <summary>
    /// DisplayConduit.DrawOverlay override.
    /// </summary>
    protected override void DrawOverlay(DrawEventArgs e)
    {
      for (var i = 0; i < OutlineCurves.Count; i++)
        e.Display.DrawCurve(OutlineCurves[i], m_color);
    }

    /// <summary>
    /// Trace the bitmap using Potrace.
    /// </summary>
    public void TraceBitmap()
    {
      Clear();
      Potrace.Clear();
      Potrace.Potrace_Trace(m_bitmap, m_path_curves);
    }

    /// <summary>
    /// Clears collections and resets the conduit bounding box.
    /// </summary>
    private void Clear()
    {
      m_bbox = BoundingBox.Unset;
      m_path_curves.Clear();
      OutlineCurves.Clear();
    }

    /// <summary>
    /// Creates outline curves from the path curves computed by Potrace.
    /// Also cooks up the conduit bounding box.
    /// </summary>
    private int CreateOutlineCurves()
    {
      OutlineCurves.Clear();

      for (var i = 0; i < m_path_curves.Count; i++)
      {
        var curve_path = m_path_curves[i];
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

        polycurve.RemoveShortSegments(m_tolerance);

        OutlineCurves.Add(polycurve);
      }

      if (OutlineCurves.Count > 0)
      {
        m_bbox = new BoundingBox();
        for (var i = 0; i < OutlineCurves.Count; i++)
          m_bbox.Union(OutlineCurves[i].GetBoundingBox(true));
      }

      // The origin of the bitmap coordinate system is at the top-left corner of the bitmap. 
      // So, create a mirror transformation so the output is oriented to Rhino's world xy plane.
      var mirror = Transform.Mirror(m_bbox.Center, Vector3d.YAxis);
      m_bbox.Transform(mirror);
      for (var i = 0; i < OutlineCurves.Count; i++)
        OutlineCurves[i].Transform(mirror);

      // Scale the output, per the calculation made in the command.
      if (m_scale != 1.0)
      {
        var scale = Transform.Scale(Point3d.Origin, m_scale);
        m_bbox.Transform(scale);
        for (var i = 0; i < OutlineCurves.Count; i++)
          OutlineCurves[i].Transform(scale);
      }

      return OutlineCurves.Count;
    }

  }
}
