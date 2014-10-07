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
   public class GetDirectoryPathFromFilePathTask : Task
   {
      [Required]
      public String FilePath { get; set; }

      [Output]
      public String DirectoryPath { get; set; }

      public override Boolean Execute()
      {
         var retVal = false;
         try
         {
            this.DirectoryPath = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( this.FilePath ) );
            retVal = true;
         }
         catch ( Exception exc )
         {
            this.Log.LogError( "Failed to get directory name from {0}: {1}.", this.FilePath, exc.Message );
         }
         return retVal;
      }
   }
}
