using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;

namespace Vectorize
{
  public class VectorizeCommand : Rhino.Commands.Command
  {
    public override string EnglishName => "Vectorize";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
      string path;
      if (mode == RunMode.Interactive)
      {
        var dialog = new Eto.Forms.OpenFileDialog();

        string[] all = { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
        dialog.Filters.Add(new Eto.Forms.FileFilter("All image files", all));

        dialog.Filters.Add(new Eto.Forms.FileFilter("Bitmap", ".bmp"));
        dialog.Filters.Add(new Eto.Forms.FileFilter("GIF", ".gif"));

        string[] jpeg = { ".jpg", ".jpe", ".jpeg" };
        dialog.Filters.Add(new Eto.Forms.FileFilter("JPEG", jpeg));
        dialog.Filters.Add(new Eto.Forms.FileFilter("PNG", ".png"));

        string[] tiff = { ".tif", ".tiff" };
        dialog.Filters.Add(new Eto.Forms.FileFilter("TIFF", tiff));

        var res = dialog.ShowDialog(RhinoEtoApp.MainWindow);
        if (res != Eto.Forms.DialogResult.Ok)
          return Result.Cancel;

        path = dialog.FileName;
      }
      else
      {
        var gs = new GetString();
        gs.SetCommandPrompt("Name of image file to open");
        gs.Get();
        if (gs.CommandResult() != Result.Success)
          return Result.Cancel;

        path = gs.StringResult();
      }

      if (!string.IsNullOrEmpty(path))
        path = path.Trim();

      if (string.IsNullOrEmpty(path))
        return Result.Failure;

      if (!File.Exists(path))
      {
        RhinoApp.WriteLine("The specified file cannot be found.");
        return Result.Failure;
      }

      var source_bitmap = Image.FromFile(path) as Bitmap;
      if (null == source_bitmap)
      {
        RhinoApp.WriteLine("The specified file cannot be identifed as a supported type.");
        return Result.Failure;
      }

      Bitmap bitmap = (Bitmap)source_bitmap.Clone();
      if (null == bitmap)
        return Result.Failure;

      bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

      if (0 == bitmap.Width || 0 == bitmap.Height)
      {
        RhinoApp.WriteLine("Error reading the specified file.");
        return Result.Failure;
      }

      // Gets the horizontal and vertical resolution, in pixels per inch
      var one_to_one_width = RhinoMath.UnsetValue;
      var one_to_one_height = RhinoMath.UnsetValue;

      var hres = bitmap.HorizontalResolution;
      var vres = bitmap.VerticalResolution;
      var us = RhinoMath.UnitScale(doc.ModelUnitSystem, UnitSystem.Inches);
      var dx = hres * us;
      var dy = vres * us;
      if (dx > 0.0 && dy > 0.0)
      {
        one_to_one_width = bitmap.Width / dx;
        one_to_one_height = bitmap.Height / dy;
      }

      GetSettings();

      var conduit = new VectorizeConduit(doc.Layers.CurrentLayer.Color) { Enabled = true };

      if (mode == RunMode.Interactive)
      {
        var dialog = new VectorizeDialog(doc, bitmap, conduit);
        dialog.RestorePosition();
        var result = dialog.ShowSemiModal(doc, RhinoEtoApp.MainWindow);
        dialog.SavePosition();
        if (result != Result.Success)
        {
          conduit.Enabled = false;
          doc.Views.Redraw();
          return Result.Cancel;
        }
      }
      else
      {
        var go = new GetOption();
        go.SetCommandPrompt("Vectorization options. Press Enter when done");
        go.AcceptNothing(true);
        while (true)
        {
          Potrace.Clear();
          conduit.ListOfPathes.Clear();
          Potrace.Potrace_Trace(bitmap, conduit.ListOfPathes);
          doc.Views.Redraw();

          go.ClearCommandOptions();

          // IgnoreArea
          var turdsize_opt = new OptionInteger(Potrace.turdsize, 2, 20);
          var turdsize_idx = go.AddOptionInteger("IgnoreArea", ref turdsize_opt, "Suppress speckles of up to this size in pixels");

          // TurnPolicy
          var turnpolicy_idx = go.AddOptionEnumList("TurnPolicy", Potrace.turnpolicy);

          // Optimizing
          var curveoptimizing_opt = new OptionToggle(Potrace.curveoptimizing, "No", "Yes");
          var curveoptimizing_idx = go.AddOptionToggle("Optimizing", ref curveoptimizing_opt);

          // Tolerance
          var opttolerance_opt = new OptionDouble(Potrace.opttolerance, 0.0, 1.0);
          var opttolerance_idx = go.AddOptionDouble("Tolerance", ref opttolerance_opt, "Corner threshold");

          // CornerThreshold
          var alphamax_opt = new OptionDouble(Potrace.alphamax, 0.0, 45.0);
          var alphamax_idx = go.AddOptionDouble("CornerThreshold", ref alphamax_opt, "Corner threshold");

          // Threshold
          var threshold_opt = new OptionDouble(Potrace.Treshold, 0.0, 100.0);
          var threshold_idx = go.AddOptionDouble("Threshold", ref threshold_opt, "Threshold");

          // RestoreDefaults
          var defaults_idx = go.AddOption("RestoreDefaults");

          var res = go.Get();

          if (res == GetResult.Option)
          {
            var option = go.Option();
            if (null != option)
            {
              if (turdsize_idx == option.Index)
                Potrace.turdsize = turdsize_opt.CurrentValue;

              if (turnpolicy_idx == option.Index)
              {
                var list = Enum.GetValues(typeof(TurnPolicy)).Cast<TurnPolicy>().ToList();
                Potrace.turnpolicy = list[option.CurrentListOptionIndex];
              }

              if (curveoptimizing_idx == option.Index)
                Potrace.curveoptimizing = curveoptimizing_opt.CurrentValue;

              if (opttolerance_idx == option.Index)
                Potrace.opttolerance = opttolerance_opt.CurrentValue;

              if (alphamax_idx == option.Index)
                Potrace.alphamax = alphamax_opt.CurrentValue;

              if (threshold_idx == option.Index)
                Potrace.Treshold = threshold_opt.CurrentValue;

              if (defaults_idx == option.Index)
                Potrace.RestoreDefaults();
            }
            continue;
          }

          if (res != GetResult.Nothing)
          {
            conduit.Enabled = false;
            doc.Views.Redraw();
            return Result.Cancel;
          }

          break;
        }
      }

      conduit.Enabled = false;

      for (var i = 0; i < conduit.ListOfPathes.Count; i++)
      {
        var list = conduit.ListOfPathes[i];
        for (var j = 0; j < list.Count; j++)
        {
          var curve = list[j].ToCurve();
          if (null != curve)
            doc.Objects.AddCurve(curve);
        }
      }

      doc.Views.Redraw();

      SetSettings();

      return Result.Success;
    }

    /// <summary>
    /// Gets the Potrace settings from the plug-in settings file.
    /// </summary>
    void GetSettings()
    {
      Potrace.RestoreDefaults();
      if (Settings.TryGetInteger("turnpolicy", out var turnpolicy))
        Potrace.turnpolicy = (TurnPolicy) turnpolicy;
      if (Settings.TryGetInteger("turdsize", out var turdsize))
        Potrace.turdsize = turdsize;
      if (Settings.TryGetDouble("alphamax", out var alphamax))
        Potrace.alphamax = alphamax;
      if (Settings.TryGetBool("curveoptimizing", out var curveoptimizing))
        Potrace.curveoptimizing = curveoptimizing;
      if (Settings.TryGetDouble("opttolerance", out var opttolerance))
        Potrace.opttolerance = opttolerance;
      if (Settings.TryGetDouble("Treshold", out var Treshold))
        Potrace.Treshold = Treshold;
    }

    /// <summary>
    /// Sets the Potrace settings to the plug-in settings file.
    /// </summary>
    void SetSettings()
    {
      Settings.SetInteger("turnpolicy", (int) Potrace.turnpolicy);
      Settings.SetInteger("turdsize", Potrace.turdsize);
      Settings.SetDouble("alphamax", Potrace.alphamax);
      Settings.SetBool("curveoptimizing", Potrace.curveoptimizing);
      Settings.SetDouble("opttolerance", Potrace.opttolerance);
      Settings.SetDouble("Treshold", Potrace.Treshold);
    }
  }
}
