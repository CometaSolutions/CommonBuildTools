/*
 * Copyright 2014 Stanislav Muhametsin. All rights Reserved.
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
using System.Net;

namespace CommonBuildTools
{

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
            using ( var webClient = new WebClient() )
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
}
