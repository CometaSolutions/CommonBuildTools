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
using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;

namespace CommonBuildTools
{
   public class MergeXMLDocsTask : Task
   {
      [Required]
      public String MainXMLDoc { get; set; }

      public ITaskItem[] XMLDocFragments { get; set; }

      public override bool Execute()
      {
         // Save main xml filename
         var mainXmlFN = this.MainXMLDoc;

         // Read main XML document
         var retVal = File.Exists( mainXmlFN );
         if ( retVal )
         {
            var mainXml = XDocument.Load( mainXmlFN );

            // Get the members node
            var membersXml = mainXml.XPathSelectElement( "/doc/members" );

            retVal = membersXml != null;
            if ( retVal )
            {

               // Iterate through xml doc fragments
               foreach ( var fragment in this.XMLDocFragments )
               {
                  // Get fragment file name from metadata, or if it doesn't exist, use full name
                  var fragmentFN = fragment.GetMetadata( "XMLDoc" ) ?? fragment.GetMetadata( "FullPath" );
                  // Skip the file if it doesn't exist
                  if ( File.Exists( fragmentFN ) )
                  {
                     // Append the children of <members> element to <members> node of main xml
                     var elems = XElement.Load( fragmentFN ).XPathSelectElements( "members/*" );
                     if ( elems.Any() )
                     {
                        membersXml.Add( elems );
                        this.Log.LogMessage( Microsoft.Build.Framework.MessageImportance.High, "Merged XML doc fragment from {0}.", fragmentFN );
                     }
                     else
                     {
                        this.Log.LogWarning( "The XML fragment file {0} does not seem to contain any documentation.", fragmentFN );
                     }
                  }
                  else
                  {
                     this.Log.LogWarning( "The XML document fragment file {0} does not exist.", fragmentFN );
                  }
               }

               // Write result to main XML document
               mainXml.Save( mainXmlFN );
            }
            else
            {
               this.Log.LogError( "The main documentation file {0} does not seem to be valid XML documentation.", mainXmlFN );
            }
         }
         else
         {
            this.Log.LogError( "The main documentation file {0} does not exist.", mainXmlFN );
         }

         return retVal;
      }
   }
}
