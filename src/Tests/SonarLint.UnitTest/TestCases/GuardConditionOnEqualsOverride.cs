﻿using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class Base
    {
        public override bool Equals(object other)
        {
            if (base.Equals(other)) // Okay; base is object
            {
                return true;
            }
            // do some checks here
        }
    }
    class Derived : Base
    {
        public override bool Equals(object other)
        {
            if (base.Equals(other))  // Noncompliant
            {
                return true;
            }
            // do some checks here
        }
    }
}
