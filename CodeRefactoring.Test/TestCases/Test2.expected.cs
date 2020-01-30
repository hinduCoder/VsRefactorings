using System;
using System.Collections.Generic;

namespace TestProject
{
    public class Foo
    {
        private readonly int length;
        private readonly string name;

        public Foo(string name, int length)
        {
            this.name = name;
            this.length = length;
        }
    }
}