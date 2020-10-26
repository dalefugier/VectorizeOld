using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;

namespace Vectorize
{
  /// <summary>
  /// Vectorize command
  /// </summary>
  public class VectorizeCommand : Command
  {
    /// <summary>
    /// Command.EnglishName override
    /// </summary>
    public override string EnglishName => "Vectorize";

    /// <summary>
    /// Command.RunCommand override
    /// </summary>
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
      Potrace.Clear();

      // Prompt the user for the name of the image file to vectorize.
      string path = GetImageFileName(mode);
      if (string.IsNullOrEmpty(path))
        return Result.Cancel;

      // Creates a bitmap from the specified file.
      var bitmap = Image.FromFile(path) as Bitmap;
      if (null == bitmap)
      {
        RhinoApp.WriteLine("The specified file cannot be identifed as a supported type.");
        return Result.Failure;
      }

      // Verify bitmap size     
      if (0 == bitmap.Width || 0 == bitmap.Height)
      {
        RhinoApp.WriteLine("Error reading the specified file.");
        return Result.Failure;
      }

      // Calculate scale factor so curves of a reasonable size are added to Rhino
      var unit_scale = (doc.ModelUnitSystem != UnitSystem.Inches)
        ? RhinoMath.UnitScale(UnitSystem.Inches, doc.ModelUnitSystem)
        : 1.0;
      var scale = (double)(1.0 / bitmap.HorizontalResolution * unit_scale);

      // I'm not convinced this is useful...
      if (true)
      {
        var format = $"F{doc.DistanceDisplayPrecision}";

        // Print image size in pixels
        RhinoApp.WriteLine("Image size in pixels: {0} x {1}",
          bitmap.Width,
          bitmap.Height
          );

        // Print image size in inches
        var width = (double)(bitmap.Width / bitmap.HorizontalResolution);
        var height = (double)(bitmap.Height / bitmap.VerticalResolution);
        RhinoApp.WriteLine("Image size in inches: {0} x {1}",
          width.ToString(format, CultureInfo.InvariantCulture),
          height.ToString(format, CultureInfo.InvariantCulture)
          );

        // Image size in in model units, if needed
        if (doc.ModelUnitSystem != UnitSystem.Inches)
        {
          width = (double)(bitmap.Width / bitmap.HorizontalResolution * unit_scale);
          height = (double)(bitmap.Height / bitmap.VerticalResolution * unit_scale);
          RhinoApp.WriteLine("Image size in {0}: {1} x {2}",
            doc.ModelUnitSystem.ToString().ToLower(),
            width.ToString(format, CultureInfo.InvariantCulture),
            height.ToString(format, CultureInfo.InvariantCulture)
            );
        }
      }

      // Convert the bitmap to an Eto bitmap
      var eto_bitmap = ConvertBitmapToEto(bitmap);
      if (null == eto_bitmap)
      {
        RhinoApp.WriteLine("Unable to convert bitmap to Eto bitmap.");
        return Result.Failure;
      }

      // This bitmap is not needed anymore, so dispose of it
      bitmap.Dispose();

      // Gets the Potrace settings from the plug-in settings file
      GetPotraceSettings();

      // Create the conduit, which does most of the work
      var conduit = new VectorizeConduit(
        eto_bitmap, 
        scale, 
        doc.ModelAbsoluteTolerance, 
        doc.Layers.CurrentLayer.Color
        ) 
      { 
        Enabled = true 
      };

      if (mode == RunMode.Interactive)
      {
        // Show the interactive dialog box
        var dialog = new VectorizeDialog(doc, conduit);
        dialog.RestorePosition();
        var result = dialog.ShowSemiModal(doc, RhinoEtoApp.MainWindow);
        dialog.SavePosition();
        if (result != Result.Success)
        {
          conduit.Enabled = false;
          Potrace.Clear();
          doc.Views.Redraw();
          return Result.Cancel;
        }
      }
      else
      {
        // Show the command line options
        var go = new GetOption();
        go.SetCommandPrompt("Vectorization options. Press Enter when done");
        go.AcceptNothing(true);
        while (true)
        {
          conduit.TraceBitmap();
          doc.Views.Redraw();

          go.ClearCommandOptions();

          // IgnoreArea
          var turdsize_opt = new OptionInteger(Potrace.turdsize, 2, 100);
          var turdsize_idx = go.AddOptionInteger("FilterSize", ref turdsize_opt, "Filter speckles of up to this size in pixels");

          // TurnPolicy
          var turnpolicy_idx = go.AddOptionEnumList("TurnPolicy", Potrace.turnpolicy);

          // Optimizing
          var curveoptimizing_opt = new OptionToggle(Potrace.curveoptimizing, "No", "Yes");
          var curveoptimizing_idx = go.AddOptionToggle("Optimizing", ref curveoptimizing_opt);

          // Tolerance
          var opttolerance_opt = new OptionDouble(Potrace.opttolerance, 0.0, 1.0);
          var opttolerance_idx = go.AddOptionDouble("Tolerance", ref opttolerance_opt, "Optimizing tolerance");

          // CornerThreshold
          var alphamax_opt = new OptionDouble(Potrace.alphamax, 0.0, 100.0);
          var alphamax_idx = go.AddOptionDouble("CornerRounding", ref alphamax_opt, "Corner rounding threshold");

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
            Potrace.Clear();
            return Result.Cancel;
          }

          break;
        }
      }

      // Group curves
      var attributes = doc.CreateDefaultAttributes();
      attributes.AddToGroup(doc.Groups.Add());
      for (var i = 0; i < conduit.OutlineCurves.Count; i++)
        doc.Objects.AddCurve(conduit.OutlineCurves[i], attributes);

      conduit.Enabled = false;
      Potrace.Clear();
      doc.Views.Redraw();

      // Set the Potrace settings to the plug -in settings file.
      SetPotraceSettings();

      return Result.Success;
    }

    /// <summary>
    /// Get name of an image file.
    /// </summary>
    protected string GetImageFileName(RunMode mode)
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
          return null;

        path = dialog.FileName;
      }
      else
      {
        var gs = new GetString();
        gs.SetCommandPrompt("Name of image file to open");
        gs.Get();
        if (gs.CommandResult() != Result.Success)
          return null;

        path = gs.StringResult();
      }

      if (!string.IsNullOrEmpty(path))
        path = path.Trim();

      if (string.IsNullOrEmpty(path))
        return null;

      if (!File.Exists(path))
      {
        RhinoApp.WriteLine("The specified file cannot be found.");
        return null;
      }

      return path;
    }

    /// <summary>
    /// Gets the Potrace settings from the plug-in settings file.
    /// </summary>
    void GetPotraceSettings()
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
    void SetPotraceSettings()
    {
      Settings.SetInteger("turnpolicy", (int) Potrace.turnpolicy);
      Settings.SetInteger("turdsize", Potrace.turdsize);
      Settings.SetDouble("alphamax", Potrace.alphamax);
      Settings.SetBool("curveoptimizing", Potrace.curveoptimizing);
      Settings.SetDouble("opttolerance", Potrace.opttolerance);
      Settings.SetDouble("Treshold", Potrace.Treshold);
    }

    /// <summary>
    /// Convert a System.Drawing.Bitmap to a Eto.Drawing.Bitmap
    /// </summary>
    Eto.Drawing.Bitmap ConvertBitmapToEto(Bitmap bitmap)
    {
      if (null == bitmap)
        return null;

      using (var stream = new MemoryStream())
      {
        bitmap.Save(stream, ImageFormat.Png);
        stream.Seek(0, SeekOrigin.Begin);
        var eto_bitmap = new Eto.Drawing.Bitmap(stream);
        return eto_bitmap;
      }
    }
  }
}
