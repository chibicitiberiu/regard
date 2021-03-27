using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Common.Utils
{
    /// <summary>
    /// A reference that can have either TLeft or TRight data type
    /// </summary>
    /// <typeparam name="TLeft">First possible datatype</typeparam>
    /// <typeparam name="TRight">Second possible datatype</typeparam>
    public class Either<TLeft, TRight>
    {
        private readonly object data;
        private readonly bool isLeft;

        /// <summary>
        /// Initializes using TLeft datatype
        /// </summary>
        /// <param name="left">Data</param>
        public Either(TLeft left)
        {
            this.data = left;
            this.isLeft = true;
        }

        /// <summary>
        /// Initializes using TRight datatype
        /// </summary>
        /// <param name="right">Data</param>
        public Either(TRight right)
        {
            this.data = right;
            this.isLeft = false;
        }

        /// <summary>
        /// If true, than this object is referencing an object of type TLeft
        /// </summary>
        public bool IsLeft => isLeft;

        /// <summary>
        /// If true, than this object is referencing an object of type TRight
        /// </summary>
        public bool IsRight => !isLeft;

        /// <summary>
        /// Gets the wrapped object as a TLeft
        /// </summary>
        public TLeft Left => (TLeft)data;

        /// <summary>
        /// Gets the wrapped object as a TRight
        /// </summary>
        public TRight Right => (TRight)data;

        /// <summary>
        /// Pattern matching
        /// </summary>
        /// <typeparam name="T">Common return type</typeparam>
        /// <param name="leftFunc">Function to apply when object is of type TLeft</param>
        /// <param name="rightFunc">Function to apply when object is of type TRight</param>
        /// <returns></returns>
        public T Match<T>(Func<TLeft, T> leftFunc, Func<TRight, T> rightFunc)
            => this.isLeft ? leftFunc(Left) : rightFunc(Right);

        /// <summary>
        /// Implicit constructor
        /// </summary>
        /// <param name="left"></param>
        public static implicit operator Either<TLeft, TRight>(TLeft left) => new Either<TLeft, TRight>(left);

        /// <summary>
        /// Implicit constructor
        /// </summary>
        /// <param name="right"></param>
        public static implicit operator Either<TLeft, TRight>(TRight right) => new Either<TLeft, TRight>(right);
    }
}
