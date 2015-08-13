﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{

    class OrderByRepeated
    {
        public void Test()
        {
            new int[] { 1, 2, 3 }.OrderBy(i => i).ThenBy(i => i); //Noncompliant
            new int[] { 1, 2, 3 }.OrderBy(i => i).ThenBy(i => i);
            new string[] { "" }
                .OrderBy(i => i, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(i => i); //Noncompliant
            new string[] { "" }
                .OrderBy(i => i, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(i => i, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(i => i); //Noncompliant
        }
    }
}
