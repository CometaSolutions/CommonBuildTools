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
using System.Xml.Linq;
using System.Xml.XPath;
using System.Collections;
using System.Xml;

namespace CommonBuildTools
{
   public class SelectXMLNodesTask : Task
   {
      [Required]
      public String XMLDoc { get; set; }

      [Required]
      public String XPath { get; set; }

      [Output]
      public ITaskItem[] XMLNodes { get; set; }

      public override Boolean Execute()
      {
         // Save XML file name
         var xmlFN = this.XMLDoc;

         var retVal = !String.IsNullOrEmpty( xmlFN );

         if ( retVal )
         {
            // Read XML file
            var xml = XDocument.Load( xmlFN );

            // Perform transformation ( XPathEvaluate will return either boolean, double, string, or IEnumerable<T> )
            var nodes = xml.XPathEvaluate( this.XPath );
            IEnumerable<String> enumerable;
            switch ( Type.GetTypeCode( nodes.GetType() ) )
            {
               case TypeCode.Boolean:
               case TypeCode.Double:
               case TypeCode.String:
                  enumerable = Enumerable.Repeat( (String) Convert.ChangeType( nodes, typeof( String ) ), 1 );
                  break;
               default:
                  // IEnumerable<T>
                  enumerable = ( (IEnumerable) nodes ).Cast<XObject>().Select( node =>
                  {
                     switch ( node.NodeType )
                     {
                        case XmlNodeType.Attribute:
                           // Default .ToString() of attribute is 'name=value' which is usually not wanted.
                           return ( (XAttribute) node ).Value;
                        default:
                           return node.ToString();
                     }
                  } );
                  break;
            }
            this.XMLNodes = enumerable
                .Select( str => new TaskItem( str ) ) // Create task item for each node
                .ToArray(); // Create an array
         }
         else
         {
            this.Log.LogError( "The XML filename path must be non-empty." );
         }

         return retVal;

      }
   }
}
