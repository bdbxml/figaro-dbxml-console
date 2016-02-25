/*************************************************************************************************
* 
* THIS CODE AND INFORMATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY
* KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
* IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
* 
*************************************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using CommandLine;
using CommandLine.Text;
using Figaro.Utilities.Commands;
using Figaro.Utilities.Common;
using CR = Figaro.Utilities.Resources.CommandResources;

namespace Figaro.Utilities
{
    class Program : IDisposable
    {
        private FigaroContext context;
        private bool ignoreErrors;
        private bool time;
        private readonly Stopwatch watch;
        private readonly List<string> originalArgs;
        static void Main(string[] args)
        {
            PrintIntro();
            using (var program = new Program(args))
            {
                try
                {
                    var parse = program.ConfigureCommandLine(args);
                    if (!parse)
                    {
                        program.Error("Failed to parse the command line. ");
                        var ht = new HelpText(new HeadingInfo(AssemblyTitle(), AssemblyVersion()));
                        ht.AddOptions(program.Options);
                        ht.AddPostOptionsLine(string.Empty);
                        program.Msg(ht.ToString());
                        return;
                    }
                }
                catch (Exception ex)
                {
                    program.Error("{0}", ex.Message);
#if DEBUG
                    program.Msg("press <enter> to exit.");
                    program.Msg(string.Empty);
                    Console.ReadLine();
#endif
                     if (!program.ignoreErrors) return;
                }

                var cmd = new StringBuilder();
                bool reading = false;


                //if we get a script, run in script mode and exit                

                if (!string.IsNullOrEmpty(program.Options.Script))
                {
                    TextReader tr = Console.In;
                    try
                    {
                        using (var sr = new StreamReader(program.Options.Script))
                        {
                            Console.SetIn(sr);
                            while (!sr.EndOfStream)
                            {
                                try
                                {
                                    var s = Console.ReadLine();
                                    if (string.IsNullOrEmpty(s)) continue;
                                    if (s.Trim().ToLower().Equals("quit") || s.Trim().ToLower().Equals("exit")) return;

                                    // if the end of the line is a space and a backslash, we're continuing the line
                                    if (s.Substring(s.Length - 2, 2).Equals(" \\"))
                                    {
                                        cmd.Append(s.Replace(" \\", string.Empty));
                                        continue;
                                    }
                                    cmd.Append(s);
                                    program.ParseCommand(cmd.ToString());
                                }
                                catch (ValidationException)
                                {
                                    // you should have handled this exception in your Command class
                                    continue;
                                }
                                catch (FigaroException fe)
                                {
                                    program.Error("[{0}], Error {1}: {2}", fe.ExceptionCategory, fe.ErrorCode,
                                                  fe.Message);
                                }
                                catch (Exception ex)
                                {
                                    program.Error("{0}", ex.Message);
                                }

                                cmd = new StringBuilder();
                            }
                            //we've reached the end of our script
                            sr.Close();
                            return;
                        }
                    }
                    finally
                    {
                        Console.SetIn(tr);
                    }
                }

                cmd = new StringBuilder();
                do
                {
                    if (!reading)
                    {
                        Console.Write(CommonResources.DBXML_PROMPT);
                        cmd = new StringBuilder();
                    }
                    try
                    {
                        var s = Console.ReadLine();
                        if (string.IsNullOrEmpty(s)) continue;
                        if (s.Substring(s.Length-1,1).Equals("\\"))
                        {
                            reading = true;
                            cmd.Append(s.Replace("\\",string.Empty));
                            continue;
                        }
                        reading = false;
                        cmd.Append(s);
                        program.ParseCommand(cmd.ToString());
                    }
#if DEBUG
                    catch (ValidationException ve)
                    {
                        // you should have handled this exception in your Command class
                        program.Error("Validation exception on {0}: {1}",ve.TargetSite.Name,ve.Message);
                    }
#endif
                    catch (FigaroException fe)
                    {
                        program.Error(CommonResources._program_error_, fe.ExceptionCategory, fe.ErrorCode, fe.Message);
                    }
                    catch (Exception ex)
                    {
                        program.Error("{0}", ex.Message);
                    }
                } while (!cmd.ToString().ToLower().Equals(CR.quit) && !cmd.ToString().ToLower().Equals(CR.exit));
            }
        }

        static void PrintIntro()
        {
            Console.Clear();
            Console.Title = AssemblyTitle();
            Console.WriteLine(AssemblyDescription());
            Console.WriteLine(CommonResources._version_ + AssemblyVersion());
            Console.WriteLine(AssemblyCopyright());
            Console.WriteLine();
        }

        #region Parser properties
        public DbxmlOptions Options { get; set; }
        #endregion

        public Program(string[] args)
        {
            originalArgs = new List<string>();
            foreach (string s in args)
            {
                originalArgs.Add(s);
            }
            watch = new Stopwatch();
            Options = new DbxmlOptions();
            ConfigureCommandLine(args);
        }

        private bool ConfigureCommandLine(string[] args)
        {
            var parser = new Parser(parserConfig => parserConfig.HelpWriter = Console.Error);
            var ret = parser.ParseArgumentsStrict(args, Options, () => Environment.Exit(-2));
            if (ret) context = new FigaroContext(Options);
            return ret;
        }

        #region ParseCommand
        private void ParseCommand(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            //NOTE: Parse in ascending length order!
            if (line.Substring(0, 1).Equals("#")) return;
            if (line.Length < 4)
            {
                Warn("invalid command: {0}", line);
                return;
            }

            if (line.Substring(0, 4).ToLower().Equals("quit") || line.Substring(0, 4).ToLower().Equals("exit")) return;

            var idx = line.IndexOf(' ');
            string cmd;
            if (idx < 0)
            {
                idx = 0;
                cmd = line;
            }
            else
            {
                cmd = line.Substring(0, idx).ToLower();
            }

            var args = idx == 0 ? string.Empty : line.Substring(idx + 1).Trim();

            if (CR.abort.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new AbortCommand();
                c.Execute(context);
                StopTimer(CR.abort + " " + args);
                return;
            }
            if (CR.addalias.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new AddAliasCommand();
                c.Execute(context, args);
                StopTimer(CR.addalias + " " + args);
                return;
            }
            if (CR.addindex.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new AddIndexCommand();
                c.Execute(context, args);
                StopTimer(CR.addindex + " " + args);
                return;
            }
            if (CR.close.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new CloseCommand();
                c.Execute(context, args);
                StopTimer(CR.close + " " + args);
                return;
            }
            if (CR.commit.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new CommitCommand();
                c.Execute(context, args);
                StopTimer(CR.commit + " " + args);
                return;
            }
            if (CR.compactcontainer.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new CompactContainerCommand();
                c.Execute(context, args);
                StopTimer(CR.compactcontainer + " " + args);
                return;
            }
            if (CR.contextquery.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new ContextQueryCommand();
                c.Execute(context, args);
                StopTimer(CR.contextquery + " " + args);
                return;
            }
            if (CR.cquery.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new CQueryCommand();
                c.Execute(context, args);
                StopTimer(CR.cquery + " " + args);
                return;
            }
            if (CR.createcontainer.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new CreateContainerCommand();
                c.Execute(context, args);
                StopTimer(CR.createcontainer + " " + args);
                return;
            }
            if (CR.delindex.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new DeleteIndexCommand();
                c.Execute(context, args);
                StopTimer(CR.delindex + " " + args);
                return;
            }
            if (CR.echo.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                Msg(args);
                return;
            }
            if (CR.getdocuments.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new GetDocumentsCommand();
                c.Execute(context, args);
                StopTimer(CR.getdocuments + " " + args);
                return;
            }
            if (CR.getmetadata.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new GetMetadataCommand();
                c.Execute(context,args);
                StopTimer(CR.getmetadata + " " + args);
                return;
            }
            if (CR.help.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                var c = new HelpCommand();
                c.Execute(args);
                return;
            }
            if (CR.info.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new InfoCommand();
                c.Execute(context, args);
                StopTimer(CR.info + " " + args);
                return;
            }
            if (CR.listindexes.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new ListIndexesCommand();
                c.Execute(context, args);
                StopTimer(CR.listindexes + " " + args);
                return;
            }
            if (CR.lookupedgeindex.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new LookupEdgeIndexCommand();
                c.Execute(context, args);
                StopTimer(CR.lookupedgeindex + " " + args);
                return;
            }
            if (CR.lookupindex.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new LookupIndexCommand();
                c.Execute(context, args);
                StopTimer(CR.lookupindex + " " + args);
                return;
            }
            if (CR.lookupstats.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new LookupStatisticsCommand();
                c.Execute(context, args);
                StopTimer(CR.lookupstats + " " + args);
                return;
            }
            if (CR.opencontainer.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new OpenContainerCommand();
                c.Execute(context, args);
                StopTimer(CR.opencontainer + " " + args);
                return;
            }
            if (CR.preload.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new PreloadCommand();
                c.Execute(context, args);
                StopTimer(CR.preload + " " + args);
                return;
            }
            if (CR.prepare.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new PrepareCommand();
                c.Execute(context, args);
                StopTimer(CR.prepare + " " + args);
                return;
            }
            if (CR.print.IndexOf(cmd, 0, StringComparison.Ordinal) == 0 || cmd.ToLower().Equals("printnames"))
            {
                StartTimer();
                var c = new PrintCommand();
                c.Execute(context, cmd.Equals("printnames") ? "printnames " + args : args);
                StopTimer(cmd.Equals("printnames") ? "printNames" : CR.print);
                return;
            }
            if (CR.putdocuments.Equals(cmd))
            {
                StartTimer();
                var c = new PutDocumentsCommand();
                c.Execute(context, args);
                StopTimer(CR.putdocuments + " " + args);
                return;
            }
            if (CR.putdocument.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new PutDocumentCommand();
                c.Execute(context, args);
                StopTimer(CR.putdocument + " " + args);
                return;
            }
            if (CR.query.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new QueryCommand();
                c.Execute(context, args);
                StopTimer(CR.query + " " + args);
                return;
            }
            if (CR.queryplan.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new QueryPlanCommand();
                c.Execute(context, args);
                StopTimer(CR.queryplan + " " + args);
                return;
            }
            if (CR.reindexcontainer.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new ReindexContainerCommand();
                c.Execute(context, args);
                StopTimer(CR.reindexcontainer + " " + args);
                return;
            }
            if (CR.removealias.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new RemoveAliasCommand();
                c.Execute(context, args);
                StopTimer(CR.removealias + " " + args);
                return;
            }
            if (CR.removecontainer.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new RemoveContainerCommand();
                c.Execute(context, args);
                StopTimer(CR.removecontainer + " " + args);
                return;
            }
            if (CR.removedocument.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new RemoveDocumentCommand();
                c.Execute(context, args);
                StopTimer(CR.removedocument+ " " + args);
                return;
            }
            if (CR.run.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                var c = new RunCommand();
                c.Execute(context, args);
                var l2 = new List<string>(originalArgs) {"-s", c.Script};
                StartTimer();
                Main(l2.ToArray());
                StopTimer(CR.run + " " + args);
                return;
            }
            if (CR.setautoindexing.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetAutoIndexingCommand();
                c.Execute(context, args);
                StopTimer(CR.setautoindexing + " " + args);
                return;
            }
            if (CR.setbaseuri.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetBaseUriCommand();
                c.Execute(context, args);
                StopTimer(CR.setbaseuri + " " + args);
                return;
            }
            if (CR.setignore.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                var c = new SetIgnoreCommand();
                c.Execute(context, args);
                ignoreErrors = c.Ignore;
                return;
            }
            if (CR.setlazy.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetLazyCommand();
                c.Execute(context, args);
                StopTimer(CR.setlazy + " " + args);
                return;
            }
            if (CR.setmetadata.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetMetadataCommand();
                c.Execute(context, args);
                StopTimer(CR.setmetadata + " " + args);
                return;
            }
            if (CR.setnamespace.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetNamespaceCommand();
                c.Execute(context, args);
                StopTimer(CR.setnamespace + " " + args);
                return;
            }
            if (CR.setprojection.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetProjectionCommand();
                c.Execute(context, args);
                StopTimer(CR.setprojection + " " + args);
                return;
            }
            if (CR.setquerytimeout.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetQueryTimeoutCommand();
                c.Execute(context, args);
                StopTimer(CR.setquerytimeout + " " + args);
                return;
            }
            if (CR.setvariable.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetVariableCommand();
                c.Execute(context, args);
                StopTimer(CR.setvariable + " " + args);
                return;
            }
            if (CR.setverbose.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new SetVerboseCommand();
                c.Execute(context, args);
                StopTimer(CR.setverbose + " " + args);
                return;
            }
            if (CR.sync.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                context.Sync();
                StopTimer(CR.sync + " " + args);
                return;
            }
            if (CR.time.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                time = true;
                ParseCommand(args);
                return;
            }
            if (CR.transaction.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new TransactionCommand();
                c.Execute(context, args);
                StopTimer(CR.transaction + " " + args);
                return;
            }
            if (CR.upgradecontainer.IndexOf(cmd, 0, StringComparison.Ordinal) == 0)
            {
                StartTimer();
                var c = new UpgradeContainerCommand();
                c.Execute(context,args);
                StopTimer(CR.upgradecontainer + " " + args);
                return;
            }

            Warn("Command not recognized: {0}", cmd);
        }
        #endregion

        #region timers
        private void StartTimer()
        {
            if (!Options.Verbose && !time ) return;
            watch.Start();
        }

        private void StopTimer(string command)
        {
            if (!Options.Verbose && !time) return;
            watch.Stop();
            Msg("command '{0}' completed in {1} seconds ({2} ms).",command.Trim(),watch.Elapsed.TotalSeconds,watch.Elapsed.TotalMilliseconds);
            watch.Reset();
            if (time) time = false;
        }
        #endregion

        #region assembly info
        /// <summary>
        /// Gets the assembly copyright.
        /// </summary>
        /// <value>The assembly copyright.</value>
        private static string AssemblyCopyright()
        {
            // Get all Copyright attributes on this assembly
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            // If there aren't any Copyright attributes, return an empty string
            return attributes.Length == 0 ? "" : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
        }

        /// <summary>
        /// Gets the assembly title.
        /// </summary>
        /// <returns>The assembly title.</returns>
        private static string AssemblyTitle()
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
            return attributes.Length == 0 ? string.Empty : ((AssemblyTitleAttribute)attributes[0]).Title;
        }

        /// <summary>
        /// Gets the assembly description.
        /// </summary>
        /// <returns>The assembly description.</returns>
        private static string AssemblyDescription()
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
            return attributes.Length == 0 ? string.Empty : ((AssemblyDescriptionAttribute)attributes[0]).Description;
        }

        /// <summary>
        /// Gets the assembly version.
        /// </summary>
        /// <returns>The assembly version.</returns>
        private static string AssemblyVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
        #endregion

        #region output
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

        protected void Msg(string msg, params object[] args)
        {
            Console.WriteLine(msg, args);
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

        #endregion

        #region Implementation of IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose() { if (context != null) context.Dispose(); }

        #endregion Implementation of IDisposable
    }
}