/*************************************************************************************************
* 
* THIS CODE AND INFORMATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY
* KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
* IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
* 
*************************************************************************************************/
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Figaro.Utilities.Common;
using Figaro.Utilities.Resources;

namespace Figaro.Utilities.Commands
{
    /// <summary>
    /// Base class for parsing commands
    /// </summary>
    public class BaseCommand
    {
        public readonly string CommandName;
        protected FigaroContext ctx;
        protected string[] argv;

        public BaseCommand(string cmd)
        {
            CommandName = cmd;
        }

        protected void WarnUsage()
        {
            Warn("Usage: {0}", CommandUsageResources.ResourceManager.GetString(CommandName));
        }

        protected void Msg(string msg, params object[] args)
        {
            if (args != null) Console.WriteLine(msg, args); else Console.WriteLine(msg);
        }

        protected void Warn(string msg, params object[] args)
        {
            var priorback = Console.BackgroundColor;
            var priorfore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg, args);
            Console.BackgroundColor = priorback;
            Console.ForegroundColor = priorfore;
            Console.WriteLine();
        }

        protected void Error(string msg, params object[] args)
        {
            var priorback = Console.BackgroundColor;
            var priorfore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg, args);
            Console.BackgroundColor = priorback;
            Console.ForegroundColor = priorfore;
            Console.WriteLine();
        }

        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        public static string[] CommandLineToArgs(string commandLine)
        {
            int argc;
            commandLine = commandLine.Replace("\"\"", string.Empty).TrimStart(new[] { ' ' });
            var argv = CommandLineToArgvW(commandLine, out argc);
            if (argv == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception();
            try
            {
                var args = new string[argc];
                for (var i = 0; i < args.Length; i++)
                {
                    var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p);
                    if (args[i][0] == '\'') args[i] = args[i].Replace("'", string.Empty);
                    if (args[i][0] == '"') args[i] = args[i].Replace("\"", string.Empty);
                }

                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }

        public void Execute(FigaroContext context, string args)
        {
            ctx = context;
            argv = CommandLineToArgs(args);
            if (argv == null || string.IsNullOrEmpty(args))
            {
                Warn("Missing parameters.");
                WarnUsage();
                throw new ValidationException();
            }
        }

        protected void ValidateIndexDescription(string index)
        {
            if (!IndexingStrategy.IsValidIndexingStrategy(index))
                throw new ValidationException(string.Format("Invalid index: {0}",index));
        }

        protected void ValidateArgCount(int min, int max)
        {
            if (argv.Length >= min && argv.Length <= max) return;
            Warn("invalid number of arguments.");
            WarnUsage();
            throw new ValidationException();
        }

        protected void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(Path.GetDirectoryName(path)))
            {
                Warn("Invalid directory or path: {0}", path);
                WarnUsage();
                throw new ValidationException();
            }

            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Warn("Directory doesn't exist: {0}", path);
                WarnUsage();
                throw new ValidationException();
            }
            return;
        }

        protected void ValidateLiteral(string arg, string[] options)
        {
            var res = from opt in options
                      where arg.ToLower().Equals(opt)
                      select opt;
            if (res.Count() > 0) return;

            Warn("invalid argument: {0}", arg);
            WarnUsage();
            throw new ValidationException();
        }
    }
}