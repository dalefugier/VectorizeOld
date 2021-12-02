using Rhino.Display;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Vectorize
{
  /// <summary>
  /// Vectorize display conduit.
  /// </summary>
  public class VectorizeConduit : DisplayConduit
  {
    private readonly List<List<Vectorize.Curve>> m_path_curves;
    private readonly Eto.Drawing.Bitmap m_bitmap;
    private readonly double m_scale;
    private readonly double m_tolerance;
    private readonly Color m_color;
    private BoundingBox m_bbox;

    /// <summary>
    /// Public constructor.
    /// </summary>
    public VectorizeConduit(Eto.Drawing.Bitmap bitmap, double scale, double tolerance, Color color)
    {
      m_path_curves = new List<List<Vectorize.Curve>>();
      m_bitmap = bitmap;
      m_scale = scale;
      m_tolerance = tolerance;
      m_color = color;
      m_bbox = BoundingBox.Unset;
      OutlineCurves = new List<Rhino.Geometry.Curve>();
    }

    /// <summary>
    /// Include a border rectangle.
    /// </summary>
    public bool IncludeBorder { get; set; } = true;

    /// <summary>
    /// The list of outline curves created from the path curves.
    /// These curve may end up in the Rhino document.
    /// </summary>
    public List<Rhino.Geometry.Curve> OutlineCurves
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
      {
        if (i == 0 && !IncludeBorder)
          continue;
        e.Display.DrawCurve(OutlineCurves[i], m_color);
      }
    }

    /// <summary>
    /// Trace the bitmap using Potrace.
    /// </summary>
    public void TraceBitmap()
    {
      Clear();
      Potrace.Clear();
      Potrace.Potrace_EtoTrace(m_bitmap, m_path_curves);
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

      // The first curve is always the border curve no matter what
      var corners = new Point3d[] {
        Point3d.Origin,
        new Point3d(m_bitmap.Width, 0.0, 0.0),
        new Point3d(m_bitmap.Width, m_bitmap.Height, 0.0),
        new Point3d(0.0, m_bitmap.Height, 0.0),
        Point3d.Origin
        };
      var border = new PolylineCurve(corners);
      OutlineCurves.Add(border);

      foreach (var path_curve in m_path_curves)
      {
        switch (GetPathCurveType(path_curve))
        {
          case PathCurveType.LineCurve:
            {
              var curve = path_curve[0].ToLineCurve();
              if (null != curve && curve.IsValid && !curve.IsShort(m_tolerance))
                OutlineCurves.Add(curve);
            }
            break;

          case PathCurveType.BezierCurve:
            {
              var curve = path_curve[0].ToNurbsCurve();
              if (null != curve && curve.IsValid && !curve.IsShort(m_tolerance))
                OutlineCurves.Add(curve);
            }
            break;

          case PathCurveType.PolylineCurve:
            {
              var points = new List<Point3d>();
              for (var i = 0; i < path_curve.Count; i++)
              {
                if (i == 0)
                  points.Add(path_curve[i].A.ToPoint3d());
                points.Add(path_curve[i].B.ToPoint3d());
              }
              var curve = new PolylineCurve(points);
              curve.MakeClosed(m_tolerance);
              curve.RemoveShortSegments(m_tolerance);
              if (curve.IsValid && !curve.IsShort(m_tolerance))
                OutlineCurves.Add(curve);
            }
            break;

          case PathCurveType.PolyCurve:
            {
              var curve = new PolyCurve();
              foreach (var path in path_curve)
              {
                if (path.Kind == CurveKind.Line)
                {
                  var c = path.ToLineCurve();
                  if (null != c && c.IsValid && !c.IsShort(m_tolerance))
                    curve.Append(c);
                }
                else
                {
                  var c = path.ToNurbsCurve();
                  if (null != c && c.IsValid && !c.IsShort(m_tolerance))
                    curve.Append(c);
                }
              }
              curve.MakeClosed(m_tolerance);
              curve.RemoveShortSegments(m_tolerance);
              if (curve.IsValid && !curve.IsShort(m_tolerance))
                OutlineCurves.Add(curve);
            }
            break;
        }
      }

      if (OutlineCurves.Count > 0)
      {
        // Just use the border curve
        m_bbox = OutlineCurves[0].GetBoundingBox(true);
        //for (var i = 0; i < OutlineCurves.Count; i++)
        //  m_bbox.Union(OutlineCurves[i].GetBoundingBox(true));
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

    private enum PathCurveType
    {
      None,
      LineCurve,
      BezierCurve,
      PolylineCurve,
      PolyCurve
    }

    private PathCurveType GetPathCurveType(IEnumerable<Vectorize.Curve> pathCurves)
    {
      if (null == pathCurves || 0 == pathCurves.Count())
        return PathCurveType.None;

      if (1 == pathCurves.Count())
      {
        return (pathCurves.First().Kind == CurveKind.Line)
          ? PathCurveType.LineCurve
          : PathCurveType.BezierCurve;
      }

      var bPolyline = true;
      foreach (var curve in pathCurves)
      {
        if (curve.Kind != CurveKind.Line)
        {
          bPolyline = false;
          break;
        }
      }

      return bPolyline
        ? PathCurveType.PolylineCurve
        : PathCurveType.PolyCurve;
    }
  }
}
