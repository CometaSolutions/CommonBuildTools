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

namespace CommonBuildTools
{
   public class ReadVersionFromFileTask : Task
   {
      [Required]
      public String VersionFilePath { get; set; }

      [Output]
      public String VersionString { get; set; }

      public override Boolean Execute()
      {
         var retVal = false;
         String fn = null;
         try
         {
            fn = System.IO.Path.GetFullPath( this.VersionFilePath );
         }
         catch ( Exception exc )
         {
            this.Log.LogError( "Failed to read version from file {0}: {1}.", this.VersionFilePath, exc.Message );
         }
         if ( fn != null )
         {
            try
            {
               this.VersionString = Version.Parse( System.IO.File.ReadAllText( fn ) ).ToString();
               retVal = true;
            }
            catch ( Exception exc )
            {
               this.Log.LogError( "Failed to read version from file {0}: {1}.", fn, exc.Message );
            }
         }
         return retVal;
      }
   }
}
