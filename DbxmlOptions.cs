// ReSharper disable UnusedAutoPropertyAccessor.Global
/*************************************************************************************************
* 
* THIS CODE AND INFORMATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY
* KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
* IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
* 
*************************************************************************************************/
using CommandLine;

namespace Figaro.Utilities
{
    public class DbxmlOptions
    {
#if !DS
        [Option('c', "create",
            Required = false,
            HelpText =
                @"  Create a new environment specified by the -h option.
                    This should only be used for debugging, since it does
                    not allow you to specify important environment configuration options"
            )]
        public bool Create { get; set; }
#endif
        [Option('h', "home",
            Required = false,
            HelpText =
                @"Specify a home directory for the database environment;
                    by default, the current working directory is used."
            )]
        public string HomeDirectory { get; set; }
#if !DS
        [Option('p', "password",
            Required = false,
            HelpText = @"   Specify an environment password.")]
        public string Password { get; set; }

#endif

        /*
         * Logging does not - and will not - capture all output, mainly because bdbxml
         * generates a lot of its own
         */
        //[Option("l", "log",
        //    Required = false,
        //    HelpText = "Logs output to the specified file. Overwrites if it exists.")]
        //public string Log;

        [Option('s', "script",
            Required = false,
            HelpText =
            @"  Execute the dbxml commands contained in the script file upon
                shell startup. The commands must be specified one to a line
                in the script file. If any of the commands contained in the
                script file fail, the shell will not start.")]
        public string Script { get; set; }
#if !DS && !CDS
        [Option('t', "transaction",
            Required = false,
            HelpText =
            @"  Transaction mode. Transactions can be used, and are required for writes.
                (Transaction support is only available in Figaro TDS and HA versions)")]
        public bool Transactional { get; set; }
#endif
        //[Option("v","version",
        //    Required = false,
        //    HelpText = @"Print the utility version.")]
        //public bool Version;

        [Option('v', "verbose",
            Required = false,
            HelpText = "Verbose option. Specifying this will increase the verbosity output.")]
        public bool Verbose { get; set; }

        [Option('z', "size", Required = false, HelpText =
            @"  If an environment is specified, set the environment cache to this size (in megabytes). Default value is 64.
                (Environments are not available in the DS version of Figaro)")]
        public int CacheSize { get; set; }

        //[HelpOption(HelpText = "display this help")]
        //public string GetHelpText()
        //{
        //    return "dbxml help";
        //}

        public DbxmlOptions()
        {
            CacheSize = 64;
        }
    }
}// ReSharper restore UnusedAutoPropertyAccessor.Global
