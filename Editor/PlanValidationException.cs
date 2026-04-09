using System;

namespace Antimatter
{
    
    public sealed class PlanValidationException : Exception
    {
        public PlanValidationException(string message) : base(message) { }
    }
}
