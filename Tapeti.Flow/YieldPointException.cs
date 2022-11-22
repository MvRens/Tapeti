﻿using System;

namespace Tapeti.Flow
{
    /// <summary>
    /// Raised when an invalid yield point is returned.
    /// </summary>
    public class YieldPointException : Exception
    {
        /// <inheritdoc />
        public YieldPointException(string message) : base(message) { }
    }
}
