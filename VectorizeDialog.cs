using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI.Controls;
using Rhino.UI.Forms;

namespace Vectorize
{
  public class VectorizeDialog : CommandDialog
  {
    private RhinoDoc m_doc;
    private System.Drawing.Bitmap m_bitmap;
    private VectorizeConduit m_conduit;
    private bool m_allow_update_and_redraw = true;

    public VectorizeDialog(RhinoDoc doc, System.Drawing.Bitmap bitmap, VectorizeConduit conduit)
    {
      m_doc = doc;
      m_bitmap = bitmap;
      m_conduit = conduit;

      Resizable = false;
      ShowHelpButton = false;
      Width = 350;
      Title = "Vectorize";
      Content = CreateTableLayout();
      Shown += (sender, e) => UpdateAndRedraw();
    }

    private RhinoDialogTableLayout CreateTableLayout()
    {

      var ns_threshold = new NumericUpDownWithUnitParsing
      {
        ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging,
        MinValue = 0.0,
        MaxValue = 100.0,
        DecimalPlaces = 0,
        Increment = 1.0,
        Value = (int) (Potrace.Treshold * 100.0),
        Width = 45
      };
      var sld_threshold = new Slider { MinValue = 0, MaxValue = 100, TickFrequency = 0, Value = (int)(Potrace.Treshold * 100.0), Width = 220 };
      sld_threshold.TickFrequency = 25;

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

      var dd_turnpolicy = new DropDown();
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

      var ns_turdsize = new  NumericUpDownWithUnitParsing { ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging, MinValue = 1.0, MaxValue = 20.0, DecimalPlaces = 0, Increment = 1.0, Value = Potrace.turdsize };
      ns_turdsize.ValueChanged += (sender, args) =>
      {
        Potrace.turdsize = (int)ns_turdsize.Value;
        UpdateAndRedraw();
      };

      var ns_alphamax = new  NumericUpDownWithUnitParsing { ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging, MinValue = 0.0, MaxValue = 45.0, DecimalPlaces = 0, Increment = 1.0, Value = Potrace.alphamax };
      ns_alphamax.ValueChanged += (sender, args) =>
      {
        Potrace.alphamax = ns_alphamax.Value;
        UpdateAndRedraw();
      };

      var chk_curveoptimizing = new CheckBox { ThreeState = false, Checked = Potrace.curveoptimizing };
      chk_curveoptimizing.CheckedChanged += (sender, args) =>
      {
        Potrace.curveoptimizing = chk_curveoptimizing.Checked.Value;
        UpdateAndRedraw();
      };

      var ns_opttolerance = new  NumericUpDownWithUnitParsing { ValueUpdateMode = NumericUpDownWithUnitParsingUpdateMode.WhenDoneChanging, MinValue = 0.1, MaxValue = 1.0, DecimalPlaces = 1, Increment = 0.1, Value = Potrace.opttolerance };
      ns_opttolerance.ValueChanged += (sender, args) =>
      {
        Potrace.opttolerance = ns_opttolerance.Value;
        UpdateAndRedraw();
      };

      var btn_reset = new Button { Text = "Restore Defaults" };
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

      var minimum_size = new Eto.Drawing.Size(150, 0);

      var layout = new RhinoDialogTableLayout(false) { Spacing = new Eto.Drawing.Size(10, 8) };
      layout.Rows.Add(new TableRow(new TableCell(new LabelSeparator { Text = "Vectorization options" }, true)));

      var panel0 = new Panel {MinimumSize = minimum_size, Content = new Label() {Text = "Threshold"}};
      var table0 = new TableLayout { Padding = new Eto.Drawing.Padding(8, 0, 0, 0) };
      table0.Rows.Add(new TableRow(new TableCell(panel0),new TableCell(sld_threshold,true), new TableCell(ns_threshold)));
      layout.Rows.Add(table0);

      var panel1 = new Panel {MinimumSize = minimum_size, Content = new Label() {Text = "Turn policy"}};
      var table2 = new TableLayout { Padding = new Eto.Drawing.Padding(8, 0, 0, 0) , Spacing = new Size(10,8)};
      table2.Rows.Add(new TableRow(new TableCell(panel1), new TableCell(dd_turnpolicy)));
      table2.Rows.Add(new TableRow(new TableCell(new Label() { Text = "Ignore area" }), new TableCell(ns_turdsize)));
      table2.Rows.Add(new TableRow(new TableCell(new Label() { Text = "Corner threshold" }), new TableCell(ns_alphamax)));
      layout.Rows.Add(table2);


      layout.Rows.Add(new TableRow(new TableCell(new LabelSeparator { Text = "Curve optimization" }, true)));

      var panel5 = new Panel {MinimumSize = minimum_size, Content = new Label() {Text = "Optimizing"}};
      var table5 = new TableLayout { Padding = new Eto.Drawing.Padding(8, 0, 0, 0), Spacing = new Size(10, 8) };
      table5.Rows.Add(new TableRow(new TableCell(panel5), new TableCell(chk_curveoptimizing)));
      table5.Rows.Add(new TableRow(new TableCell(new Label() { Text = "Tolerance" }), new TableCell(ns_opttolerance)));
      layout.Rows.Add(table5);

   


      return layout;
    }

    private void UpdateAndRedraw()
    {
      if (m_allow_update_and_redraw && null != m_doc && null != m_bitmap && null != m_conduit)
      {
        Potrace.Clear();
        m_conduit.ListOfPathes.Clear();
        Potrace.Potrace_Trace(m_bitmap, m_conduit.ListOfPathes);
        m_doc.Views.Redraw();
      }
    }
  }
}
