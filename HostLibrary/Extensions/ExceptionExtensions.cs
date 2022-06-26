using System;
using System.Text;

namespace HostLibrary.Extensions
{
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Возвращает текст исключений: указанного и вложенных.
        /// </summary>
        /// <param name="includeExceptionType">Включать ли информацию о типе исключений.</param>
        /// <param name="includeStackTrace">Включать ли stack trace по каждому исключению.</param>
        public static string GetAllMessages(this Exception ex, bool includeExceptionType, bool includeStackTrace)
        {
            const string innerPrefix = "\nInner exception: ";
            var builder = new StringBuilder();
            var current = ex;
            bool isInner = false;

            while (current != null)
            {
                var localMessage = current.Message;

                builder.AppendFormat("{3}{0}{1}{2}",
                    localMessage,
                    includeExceptionType ? $" ({current.GetType()})" : string.Empty,
                    includeStackTrace ? "\r\n" + current.StackTrace : string.Empty,
                    isInner ? innerPrefix : string.Empty);

                current = current.InnerException;
                isInner = true;
            }
            var message = builder.ToString();
            return message;
        }
    }
}
