using System;

namespace MipSdkHelper
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}