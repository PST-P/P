// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Plang.CSharpRuntime.Values;

namespace PImplementation
{
    public sealed class tObserver : IPrtValue
    {
        private static tObserver _instance;
        private Dictionary<string, string> _states = new Dictionary<string, string>();

        // Singleton pattern
        private tObserver()
        {
        }

        public static tObserver GetInstance()
        {
            if (_instance == null)
            {
                _instance = new tObserver();
            }

            return _instance;
        }

        public static void SetState(PrtString m, PrtString s)
        {
            GetInstance()._states[m] = s;
        }

        public static PrtString GetState(PrtString m)
        {
            return GetInstance()._states.GetValueOrDefault(m, "none");
        }

        public bool Equals(IPrtValue other)
        {
            return other is tObserver;
        }

        public IPrtValue Clone()
        {
            var cloned = new tObserver();
            return cloned;
        }

        public string ToEscapedString()
        {
            return "Observer";
        }
    }
}
