using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI.Controls;
using Rhino.UI.Forms;

namespace Vectorize
{
  /// <summary>
  /// Vectorize command dialog
  /// </summary>
  public class VectorizeDialog : CommandDialog
  {
    private RhinoDoc m_doc;
    private VectorizeConduit m_conduit;
    private bool m_allow_update_and_redraw = true;

    /// <summary>
    /// Public constructor
    /// </summary>
    public VectorizeDialog(RhinoDoc doc, VectorizeConduit conduit)
    {
      m_doc = doc;
      m_conduit = conduit;

      Resizable = false;
      ShowHelpButton = false;
      Width = 350;
      Title = "Vectorize";
      Content = CreateTableLayout();
      Shown += (sender, e) => UpdateAndRedraw();
    }

    /// <summary>
    /// Creates the content of the dialog
    /// </summary>
    private RhinoDialogTableLayout CreateTableLayout()
    {
      // Create controls and define behaviors

      var ns_threshold = new NumericUpDownWithUnitParsing
      {
        ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging,
        MinValue = 0.0,
        MaxValue = 100.0,
        DecimalPlaces = 0,
        Increment = 1.0,
        ToolTip = "Weighted RGB color evaluation threshold.",
        Value = (int)(Potrace.Treshold * 100.0), Width = 45
      };

      var sld_threshold = new Slider
      {
        MinValue = 0,
        MaxValue = 100,
        TickFrequency = 25,
        Value = (int)(Potrace.Treshold * 100.0),
        Width = 220
      };

      ns_threshold.ValueChanged += (sender, args) =>
      {
        if (m_allow_update_and_redraw)
        {
          m_allow_update_and_redraw = false;
          Potrace.Treshold = ns_threshold.Value / 100.0;
          sld_threshold.Value = (int)(Potrace.Treshold * 100.0);
          m_allow_update_and_redraw = true;
          UpdateAndRedraw();
        }
      };

      sld_threshold.ValueChanged += (sender, args) =>
      {
        if (m_allow_update_and_redraw)
        {
          m_allow_update_and_redraw = false;
          Potrace.Treshold = sld_threshold.Value / 100.0;
          ns_threshold.Value = (int)(Potrace.Treshold * 100.0);
          m_allow_update_and_redraw = true;
          UpdateAndRedraw();
        }
      };

      var dd_turnpolicy = new DropDown
      {
        ToolTip = "Algorithm used to resolve ambiguities in path decomposition."
      };
      foreach (var str in Enum.GetNames(typeof(TurnPolicy)))
        dd_turnpolicy.Items.Add(str);
      dd_turnpolicy.SelectedIndex = (int)Potrace.turnpolicy;
      dd_turnpolicy.SelectedIndexChanged += (sender, args) =>
      {
        if (dd_turnpolicy.SelectedIndex != 0)
        {
          Potrace.turnpolicy = (TurnPolicy)dd_turnpolicy.SelectedIndex;
          UpdateAndRedraw();
        }
      };

      var ns_turdsize = new NumericUpDownWithUnitParsing 
      { 
        ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging, 
        MinValue = 1.0, 
        MaxValue = 100.0, 
        DecimalPlaces = 0, 
        Increment = 1.0,
        ToolTip = "Filter speckles of up to this size in pixels.",
        Value = Potrace.turdsize 
      };
      ns_turdsize.ValueChanged += (sender, args) =>
      {
        Potrace.turdsize = (int)ns_turdsize.Value;
        UpdateAndRedraw();
      };

      var ns_alphamax = new NumericUpDownWithUnitParsing 
      { 
        ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging, 
        MinValue = 0.0, 
        MaxValue = 100.0, 
        DecimalPlaces = 0, 
        Increment = 1.0,
        ToolTip = "Corner rounding threshold.",
        Value = Potrace.alphamax 
      };
      ns_alphamax.ValueChanged += (sender, args) =>
      {
        Potrace.alphamax = ns_alphamax.Value;
        UpdateAndRedraw();
      };

      var chk_curveoptimizing = new CheckBox 
      { 
        ThreeState = false,
        ToolTip = "Optimize of Bézier segments by a single segment when possible.",
        Checked = Potrace.curveoptimizing 
      };

      var ns_opttolerance = new NumericUpDownWithUnitParsing 
      { 
        ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging, 
        MinValue = 0.1, 
        MaxValue = 1.0, 
        DecimalPlaces = 1, 
        Increment = 0.1,
        Enabled = Potrace.curveoptimizing,
        ToolTip = "Tolerance used to optimize Bézier segments.",
        Value = Potrace.opttolerance 
      };

      chk_curveoptimizing.CheckedChanged += (sender, args) =>
      {
        Potrace.curveoptimizing = chk_curveoptimizing.Checked.Value;
        ns_opttolerance.Enabled = Potrace.curveoptimizing;
        UpdateAndRedraw();
      };

      ns_opttolerance.ValueChanged += (sender, args) =>
      {
        Potrace.opttolerance = ns_opttolerance.Value;
        UpdateAndRedraw();
      };

      var btn_reset = new Button 
      { 
        Text = "Restore Defaults" 
      };
      btn_reset.Click += (sender, args) =>
      {
        m_allow_update_and_redraw = false;
        Potrace.RestoreDefaults();
        sld_threshold.Value = (int)(Potrace.Treshold * 100.0);
        dd_turnpolicy.SelectedIndex = (int)Potrace.turnpolicy;
        ns_turdsize.Value = Potrace.turdsize;
        ns_alphamax.Value = Potrace.alphamax;
        chk_curveoptimizing.Checked = Potrace.curveoptimizing;
        ns_opttolerance.Value = Potrace.opttolerance;
        m_allow_update_and_redraw = true;
        UpdateAndRedraw();
      };

      // Layout the controls

      var minimum_size = new Eto.Drawing.Size(150, 0);

      var layout = new RhinoDialogTableLayout(false) { Spacing = new Eto.Drawing.Size(10, 8) };
      layout.Rows.Add(new TableRow(new TableCell(new LabelSeparator { Text = "Vectorization options" }, true)));

      var panel0 = new Panel {MinimumSize = minimum_size, Content = new Label() {Text = "Threshold"}};
      var table0 = new TableLayout { Padding = new Eto.Drawing.Padding(8, 0, 0, 0) };
      table0.Rows.Add(new TableRow(new TableCell(panel0),new TableCell(sld_threshold,true), new TableCell(ns_threshold)));
      layout.Rows.Add(table0);

      var panel1 = new Panel {MinimumSize = minimum_size, Content = new Label() {Text = "Turn policy"}};
      var table1 = new TableLayout { Padding = new Eto.Drawing.Padding(8, 0, 0, 0) , Spacing = new Size(10,8)};
      table1.Rows.Add(new TableRow(new TableCell(panel1), new TableCell(dd_turnpolicy)));
      table1.Rows.Add(new TableRow(new TableCell(new Label() { Text = "Filter size" }), new TableCell(ns_turdsize)));
      table1.Rows.Add(new TableRow(new TableCell(new Label() { Text = "Corner rounding" }), new TableCell(ns_alphamax)));
      layout.Rows.Add(table1);

      layout.Rows.Add(new TableRow(new TableCell(new LabelSeparator { Text = "Curve optimization" }, true)));

      var panel2 = new Panel {MinimumSize = minimum_size, Content = new Label() {Text = "Optimizing"}};
      var table2 = new TableLayout { Padding = new Eto.Drawing.Padding(8, 0, 0, 0), Spacing = new Size(10, 8) };
      table2.Rows.Add(new TableRow(new TableCell(panel2), new TableCell(chk_curveoptimizing)));
      table2.Rows.Add(new TableRow(new TableCell(new Label() { Text = "Tolerance" }), new TableCell(ns_opttolerance)));
      table2.Rows.Add(null);
      table2.Rows.Add(new TableRow(new TableCell(new Label() { Text = "" }), new TableCell(btn_reset)));
      layout.Rows.Add(table2);

      return layout;
    }

    private void UpdateAndRedraw()
    {
      if (m_allow_update_and_redraw && null != m_doc && null != m_conduit)
      {
        m_conduit.TraceBitmap();
        m_doc.Views.Redraw();
      }
    }
  }
}
