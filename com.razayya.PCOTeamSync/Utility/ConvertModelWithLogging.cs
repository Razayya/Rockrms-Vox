using System;
using Rock;

namespace com.razayya.PCOTeamSync.Utility
{
    public static class Converter
    {
        /// <summary>
        /// Executes a delegate function (which converts between models), intercepting any errors in the delegate
        /// to make the error message more meaningful and log the error.
        /// </summary>
        /// <typeparam name="T">The typeparam.</typeparam>
        /// <param name="importRecord">The import record the delegate is working with.</param>
        /// <param name="delegateFunction">The delegate function.</param>
        /// <param name="includeSourceObjectInError">if set to <c>true</c>, includes the importRecord JSON in logged error.</param>
        /// <returns>The result of the delegate.</returns>
        public static T ConvertModelWithLogging<T>( object importRecord, Func<T> delegateFunction, bool includeSourceObjectInError = true )
        {
            try
            {
                return delegateFunction();
            }
            catch (Exception ex)
            {
                string inputType = importRecord.GetType().FullName;
                string outputType = typeof( T ).FullName;
                string exMessage = $"Error converting from {inputType} to {outputType}.";
                if (includeSourceObjectInError)
                {
                    string inputObject = importRecord.ToJson();
                    exMessage = exMessage + $"  Input object: {inputObject}.";
                }
                var logException = new Exception( exMessage, ex );
                throw logException;
            }
        }
    }
}
