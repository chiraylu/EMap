﻿using System;
using System.Collections.Generic;
using System.Text;

namespace EM.GIS.Data
{
    /// <summary>
    /// 进度处理类
    /// </summary>
    public class ProgressHandler : IProgressHandler
    {
        public Action<int, string> Handler { get; set; }
        public void Progress(int percent, string message = null)
        {
            Handler?.Invoke(percent, message);
        }
    }
}
