using Rhino;
using Rhino.PlugIns;

namespace Vectorize
{
  public class VectorizePlugIn : PlugIn
  {
    public VectorizePlugIn()
    {
      Instance = this;
    }

    public static VectorizePlugIn Instance
    {
      get; private set;
    }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
      var ver = RhinoApp.ExeVersion;
      if (ver > 7)
      {
        RhinoApp.WriteLine("Vectorize is included with Rhino {0}.", ver);
        return LoadReturnCode.ErrorNoDialog;
      }
      return LoadReturnCode.Success;
    }
  }
}