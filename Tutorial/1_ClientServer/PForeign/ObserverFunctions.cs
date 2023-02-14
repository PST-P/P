using System;
using Plang.CSharpRuntime;
using Plang.CSharpRuntime.Values;

namespace PImplementation
{
    public static partial class GlobalFunctions
    {
        public static void SetState(PrtString m, PrtString s, PMachine machine)
        {
            machine.LogLine("Observer: setting state " + s + " to machine " + m);
            tObserver.SetState(m, s);
        }

        public static PrtString GetState(PrtString m, PMachine machine)
        {
            machine.LogLine("Observer: getting state from machine " + m);
            return tObserver.GetState(m);
        }
    }
}