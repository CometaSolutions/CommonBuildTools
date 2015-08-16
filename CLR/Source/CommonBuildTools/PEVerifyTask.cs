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
using System.Linq;
using System.Text;

namespace CommonBuildTools
{
   public class PEVerifyTask : ToolTask
   {
      private const String PEVERIFY = "PEVerify.exe";

      [Required]
      public String FileToVerify { get; set; }

      public Boolean NoMDVerification { get; set; }
      public Boolean NoILVerification { get; set; }
      public Boolean OnlyTransparentMethods { get; set; }
      public Boolean OnlyUniqueErrors { get; set; }
      public Boolean NoHResultCodes { get; set; }
      public Boolean MeasureVerificationTimes { get; set; }
      public String IgnoreErrorCodes { get; set; }
      public String IgnoreErrorFile { get; set; }
      public Boolean Quiet { get; set; }
      public Boolean NoVerbose { get; set; }
      public Boolean ShowLogo { get; set; }

      protected override String GenerateFullPathToTool()
      {
         String peVerifyPath = null;
         try
         {
            peVerifyPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile( PEVERIFY, TargetDotNetFrameworkVersion.VersionLatest, DotNetFrameworkArchitecture.Bitness64 );
         }
         catch
         {
            this.Log.LogMessage( MessageImportance.Low, "Failed to get 64bit WinSDK path, trying 32bit." );
            try
            {
               peVerifyPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile( PEVERIFY, TargetDotNetFrameworkVersion.VersionLatest, DotNetFrameworkArchitecture.Bitness32 );
            }
            catch ( Exception exc )
            {
               this.Log.LogError( "Failed to get Windows SDK path for PEVerify: {0}.", exc.Message );
            }
         }

         return peVerifyPath;
      }

      protected override String ToolName
      {
         get
         {
            return PEVERIFY;
         }
      }

      protected override MessageImportance StandardOutputLoggingImportance
      {
         get
         {
            return MessageImportance.Normal;
         }
      }

      protected override String GenerateCommandLineCommands()
      {
         var builder = new CommandLineBuilder();

         builder.AppendFileNameIfNotNull( this.FileToVerify );
         if ( !this.NoMDVerification )
         {
            builder.AppendSwitch( "/MD " );
         }

         if ( !this.NoILVerification )
         {
            builder.AppendSwitch( "/IL " );
         }

         if ( this.OnlyTransparentMethods )
         {
            builder.AppendSwitch( "/TRANSPARENT " );
         }

         if ( this.OnlyUniqueErrors )
         {
            builder.AppendSwitch( "/UNIQUE " );
         }

         if ( !this.NoHResultCodes )
         {
            builder.AppendSwitch( "/HRESULT " );
         }

         if ( this.MeasureVerificationTimes )
         {
            builder.AppendSwitch( "/CLOCK" );
         }

         if ( !String.IsNullOrEmpty( this.IgnoreErrorCodes ) )
         {
            builder.AppendSwitchIfNotNull( "/IGNORE=", this.IgnoreErrorCodes );
         }
         else if ( !String.IsNullOrEmpty( this.IgnoreErrorFile ) )
         {
            builder.AppendSwitchIfNotNull( "/IGNORE=@", this.IgnoreErrorFile );
         }

         if ( this.Quiet )
         {
            builder.AppendSwitch( "/QUIET " );
         }

         if ( !this.NoVerbose )
         {
            builder.AppendSwitch( "/VERBOSE " );
         }

         if ( !this.ShowLogo )
         {
            builder.AppendSwitch( "/NOLOGO " );
         }

         return builder.ToString();

      }
   }
}
