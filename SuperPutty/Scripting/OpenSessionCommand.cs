﻿/*
 * Copyright (c) 2009 - 2015 Jim Radford http://www.jimradford.com
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"}, to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions: 
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using log4net;
using SuperPutty.App;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPutty.Scripting
{
    public static partial class Commands
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Commands));

        /// <summary>Open a new session</summary>
        /// <param name="arg">The name of ID of the session to start</param>
        /// <returns>null</returns>
        internal static CommandData OpenSessionHandler(string arg)
        {            
            
            SessionData session = SuperPuTTY.GetSessionById(arg);
            if (session != null)
            {
                SuperPuTTY.OpenSession(new SessionDataStartInfo() { Session = session });
            }
            else
            {
                Log.WarnFormat("Could not start session named {0}, does it exist?", arg);
            }
            return null;
        }
    }
}
