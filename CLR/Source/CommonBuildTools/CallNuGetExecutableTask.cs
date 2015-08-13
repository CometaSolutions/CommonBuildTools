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
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CommonBuildTools
{
   // This class is here because CallNuGetExecutableTask is here as well (and it uses DownloadFileTask class).
   public class DownloadFileTask : Task
   {
      [Required]
      public String URI { get; set; }

      [Required]
      public String FilePath { get; set; }

      public override Boolean Execute()
      {
         return DoDownload( this.URI, this.FilePath, this.Log );
      }

      internal static Boolean DoDownload( String uri, String filePath, TaskLoggingHelper log )
      {
         var retVal = false;
         try
         {
            var fn = System.IO.Path.GetFullPath( filePath );

            log.LogMessage( MessageImportance.High, "Downloading from {0} to {1}.", uri, fn );
            using ( var webClient = new System.Net.WebClient() )
            {
               webClient.DownloadFile( uri, fn );
            }
            retVal = true;
         }
         catch ( Exception exc )
         {
            log.LogErrorFromException( exc );
         }

         return retVal;
      }
   }

   public class CallNuGetExecutableTask : ToolTask
   {
      public String NuGetExecutable { get; set; }
      public String NuGetArguments { get; set; }

      protected override String GenerateFullPathToTool()
      {
         var nugetExe = this.NuGetExecutable;
         if ( String.IsNullOrEmpty( nugetExe ) )
         {
            nugetExe = Path.Combine( Environment.CurrentDirectory, "NuGet.exe" );
            this.Log.LogMessage( MessageImportance.High, "A path to NuGet executable was not specified, using {0}.", nugetExe );
         }
         else
         {
            nugetExe = Path.GetFullPath( nugetExe );
         }

         if ( !File.Exists( nugetExe ) )
         {
            var uri = "https://www.nuget.org/nuget.exe";
            this.Log.LogMessage( MessageImportance.High, "NuGet executable {0} does not exist, downloading from {1}.", nugetExe, uri );
            if ( !DownloadFileTask.DoDownload( uri, nugetExe, this.Log ) )
            {
               this.Log.LogWarning( "There was an error while downloading file, subsequent NuGet calls will most likely fail." );
            }
         }

         return nugetExe;
      }

      protected override String ToolName
      {
         get
         {
            return "NuGet.exe";
         }
      }

      protected override String GenerateCommandLineCommands()
      {
         return this.NuGetArguments;
      }
   }
}
