/*
 * Copyright 2015 Stanislav Muhametsin. All rights Reserved.
 *
 * Licensed  under the  Apache License,  Version 2.0  (the "License");
 * you may not use  this file  except in  compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed  under the  License is distributed on an "AS IS" BASIS,
 * WITHOUT  WARRANTIES OR CONDITIONS  OF ANY KIND, either  express  or
 * implied.
 *
 * See the License for the specific language governing permissions and
 * limitations under the License. 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;

namespace CommonBuildTools
{
   // For some reason, MSBuild.Community.Tasks.NUnit task does not have customizable version (only way to customize that is through ToolPath, which doesn't work well in version controlled environment...)
   public class NUnitTask : ToolTask
   {
      private const String REG_KEY = @"HKEY_CURRENT_USER\Software\nunit.org\Nunit\";

      // Newest at the moment
      private const String DEFAULT_NUNIT_VERSION = "2.6.4";

      [Required]
      public ITaskItem[] Assemblies { get; set; }

      public String ConfigurationFile { get; set; }

      public String IncludeCategories { get; set; }

      public String ExcludeCategories { get; set; }

      public String StandardOutputFile { get; set; }

      public String ErrorOutputFile { get; set; }

      public Boolean Labels { get; set; }

      public String TestOutputFile { get; set; }

      public String TestOutputXSLTFile { get; set; }

      public String NUnitVersion { get; set; }

      public Boolean NoShadowAssemblies { get; set; }

      public Boolean RawXMLOutput { get; set; }

      public Boolean Use32BitProcess { get; set; }

      protected override String GenerateFullPathToTool()
      {
         var filePath = this.ToolPath;

         if ( String.IsNullOrEmpty( filePath ) )
         {
            // Try to deduce
            var version = this.NUnitVersion;
            if ( String.IsNullOrEmpty( version ) )
            {
               version = DEFAULT_NUNIT_VERSION;
            }

            var tmp = Path.Combine(
               Environment.GetFolderPath( Environment.SpecialFolder.ProgramFilesX86 ),
               "NUnit " + version
               );
            if ( !Directory.Exists( tmp ) )
            {
               // Try registry
               try
               {
                  tmp = Convert.ToString( Microsoft.Win32.Registry.GetValue( REG_KEY + version, "InstallDir", tmp ) );
               }
               catch ( Exception exc )
               {
                  this.Log.LogError( "Error when getting NUnit {0} install directory from registry: {1}.", version, exc.Message );
               }
            }

            if ( !String.IsNullOrEmpty( tmp ) && Directory.Exists( tmp ) )
            {
               filePath = Path.Combine( filePath, "bin" );
            }
         }

         if ( String.IsNullOrEmpty( filePath ) )
         {
            this.Log.LogError( "Failed to find NUnit executable." );
         }

         return filePath;
      }

      protected override String ToolName
      {
         get
         {
            var exe = "nunit-console";
            if ( this.Use32BitProcess )
            {
               exe += "-x86";
            }

            if ( Environment.OSVersion.Platform != PlatformID.Unix )
            {
               exe += ".exe";
            }

            return exe;
         }
      }

      protected override String GenerateCommandLineCommands()
      {
         var cmd = new CommandLineBuilder();

         // First, assemblies
         cmd.AppendFileNamesIfNotNull( this.Assemblies, " " );

         // Then, options
         if ( this.NoShadowAssemblies )
         {
            cmd.AppendSwitch( "/noshadow" );
         }

         if ( this.Labels )
         {
            cmd.AppendSwitch( "/labels" );
         }

         if ( this.RawXMLOutput )
         {
            cmd.AppendSwitch( "/xmlconsole" );
         }

         cmd.AppendSwitchIfNotNull( "/config:", this.ConfigurationFile );

         cmd.AppendSwitchIfNotNull( "/include:", this.IncludeCategories );

         cmd.AppendSwitchIfNotNull( "/exclude:", this.ExcludeCategories );

         cmd.AppendSwitchIfNotNull( "/out:", this.StandardOutputFile );

         cmd.AppendSwitchIfNotNull( "/err:", this.ErrorOutputFile );

         cmd.AppendSwitchIfNotNull( "/xml:", this.TestOutputFile );

         cmd.AppendSwitchIfNotNull( "/transform:", this.TestOutputXSLTFile );

         return cmd.ToString();

      }
   }
}
