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
using System.Xml.Linq;
using Microsoft.Build.Framework;
using System.IO;

namespace CommonBuildTools
{
   public class GenerateNuSpecFileTask : Task
   {
      private const String ITEM_MD_TARGET_PATH = "TargetFilePath";
      private const String ITEM_MD_TARGET_FW = "TargetFramework";
      private const String ITEM_MD_EXPLICIT_REF = "ExplicitReference";
      private const String ITEM_MD_VERSION = "Version";
      private const String ITEM_MD_EXCLUDE = "Exclude";

      // MSBuild doesn't support nullable stuff...
      private Int32? _nullable_CIY;
      private Boolean? _nullable_RLA;
      private Boolean? _nullable_DD;

      // One of these should be specified
      public String VersionFile { get; set; }
      public String VersionContents { get; set; }

      // If Copyright is null or empty, and this is specified, then automatic copyright is generated
      public Int32 Copyright_InceptionYear
      {
         get
         {
            return this._nullable_CIY ?? 0;
         }
         set
         {
            this._nullable_CIY = value;
         }
      }
      public String Copyright { get; set; }

      [Required]
      public String PackageID { get; set; }
      [Required]
      public String Authors { get; set; }
      [Required]
      public String Description { get; set; }
      [Required]
      public ITaskItem[] Files { get; set; }

      // One of these should be specified
      public String OutputPath { get; set; }
      public String OutputDirectory { get; set; }

      public String Title { get; set; }
      public String Owners { get; set; }
      public String ReleaseNotes { get; set; }
      public String Tags { get; set; }
      public String Summary { get; set; }
      public String Language { get; set; }
      public String ProjectURL { get; set; }
      public String IconURL { get; set; }
      public String LicenseURL { get; set; }
      public Boolean RequireLicenseAcceptance
      {
         get
         {
            return this._nullable_RLA ?? false;
         }
         set
         {
            this._nullable_RLA = value;
         }
      }
      public Boolean DevelopmentDependency
      {
         get
         {
            return this._nullable_DD ?? false;
         }
         set
         {
            this._nullable_DD = value;
         }
      }
      public ITaskItem[] Dependencies { get; set; }
      public ITaskItem[] FrameworkAssemblies { get; set; }

      public override Boolean Execute()
      {
         var version = this.VersionContents;
         if ( String.IsNullOrEmpty( version ) )
         {
            var versionFile = this.VersionFile;
            if ( String.IsNullOrEmpty( versionFile ) )
            {
               this.Log.LogError( "Either file containing version string, or version contents should be specified." );
            }
            else
            {
               version = File.ReadAllText( versionFile );
            }
         }

         var id = this.CheckMandatoryContent( "id", this.PackageID );
         var authors = this.CheckMandatoryContent( "authors", this.Authors );
         var description = this.CheckMandatoryContent( "description", this.Description );
         var outputPath = this.OutputPath;
         if ( String.IsNullOrEmpty( outputPath ) && !String.IsNullOrEmpty( id ) )
         {
            var outDir = this.OutputDirectory;
            if ( String.IsNullOrEmpty( outDir ) )
            {
               this.Log.LogError( "Either output file, or output directory should be specified." );
            }
            else
            {
               outputPath = Path.Combine( outDir, id + ".nuspec" );
            }
         }

         var retVal = !String.IsNullOrEmpty( version )
            && !String.IsNullOrEmpty( id )
            && !String.IsNullOrEmpty( authors )
            && !String.IsNullOrEmpty( description )
            && !String.IsNullOrEmpty( outputPath );
         if ( retVal )
         {
            // Metadata
            var md = new XElement( "metadata" );
            AddElement( md, "id", id );
            AddElement( md, "version", version );
            AddElementIfPresent( md, "title", this.Title );
            AddElement( md, "authors", authors );
            AddElementIfPresent( md, "owners", this.Owners );
            AddElement( md, "description", description );
            AddElementIfPresent( md, "releaseNotes", this.ReleaseNotes );
            AddElementIfPresent( md, "summary", this.Summary );
            AddElementIfPresent( md, "language", this.Language );
            AddElementIfPresent( md, "projectUrl", this.ProjectURL );
            AddElementIfPresent( md, "iconUrl", this.IconURL );
            AddElementIfPresent( md, "licenseUrl", this.LicenseURL );
            AddElementIfPresent( md, "copyright", this.ConstructCopyright() );
            AddElementIfPresent( md, "requireLicenseAcceptance", this._nullable_RLA );
            AddElementIfPresent( md, "tags", this.Tags );
            AddElementIfPresent( md, "developmentDependency", this._nullable_DD );

            this.AddDependencies( md );
            this.AddReferences( md );
            this.AddTargetFWAssemblies( md );

            // Top-level element
            XNamespace ns = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd";
            var nuspec = new XElement( ns + "package", md );

            // Files
            this.AddFiles( nuspec );

            // Save
            var outDir = Path.GetDirectoryName( outputPath );
            if ( !Directory.Exists( outDir ) )
            {
               Directory.CreateDirectory( outDir );
            }

            new XDocument( new XDeclaration( "1.0", "utf-8", "yes" ), nuspec )
               .Save( outputPath );
         }
         else
         {
            this.Log.LogError( "One or more of the required parameters was not specified." );
         }

         return retVal;
      }

      private String CheckMandatoryContent( String name, String content )
      {
         if ( String.IsNullOrEmpty( content ) )
         {
            this.Log.LogError( "NuSpec element {0} did not have a content.", name );
         }
         return content;
      }

      private String ConstructCopyright()
      {
         var cr = this.Copyright;
         var inceptionNullable = this._nullable_CIY;
         if ( String.IsNullOrEmpty( cr ) && inceptionNullable.HasValue )
         {
            var inception = inceptionNullable.Value;
            cr = "Copyright © " + inception;
            var now = DateTime.Now.Year;
            if ( now > inception )
            {
               cr += "-" + now + " ";
            }
            cr += this.Authors + ". All rights reserved.";
         }
         return cr;
      }

      private static void AddElement( XElement parent, String elementName, String content )
      {
         parent.Add( new XElement( elementName, content ) );
      }

      private static void AddElementIfPresent( XElement parent, String elementName, String content )
      {
         if ( !String.IsNullOrEmpty( content ) )
         {
            AddElement( parent, elementName, content );
         }
      }

      private static void AddElementIfPresent( XElement parent, String elementName, Boolean? content )
      {
         if ( content.HasValue )
         {
            AddElement( parent, elementName, content.Value.ToString() );
         }
      }

      private void AddDependencies( XElement parent )
      {
         var dependencies = this.Dependencies;
         if ( dependencies != null && dependencies.Length > 0 )
         {
            parent.Add( new XElement( "dependencies", dependencies
               .GroupBy( d => d.GetMetadata( ITEM_MD_TARGET_FW ) ?? String.Empty )
               .Select( g =>
               {
                  var grpX = new XElement( "group", g.Select( d =>
                  {
                     var depX = new XElement( "dependency", new XAttribute( "id", d.ItemSpec ) );
                     var version = d.GetMetadata( ITEM_MD_VERSION );
                     if ( !String.IsNullOrEmpty( version ) )
                     {
                        depX.SetAttributeValue( "version", version );
                     }
                     return depX;
                  } ) );
                  if ( !String.IsNullOrEmpty( g.Key ) )
                  {
                     grpX.SetAttributeValue( "targetFramework", g.Key );
                  }

                  return grpX;

               } ) ) );
         }
      }

      private void AddReferences( XElement parent )
      {
         var files = this.Files;
         if ( files != null && files.Length > 0 )
         {
            var refs = files
               .Where( f => f.GetMetadata( ITEM_MD_EXPLICIT_REF ).ParseAsBooleanSafe() )
               .Select( f => f.GetMetadata( ITEM_MD_TARGET_PATH ) )
               .ToArray();
            if ( refs.Length > 0 )
            {
               parent.Add( new XElement( "references", refs.GroupBy( r =>
               {
                  // Extract "net40" from e.g. lib\net40\MyAssembly.dll
                  var targetDir = Path.GetDirectoryName( r );
                  String grp;
                  if ( String.IsNullOrEmpty( targetDir ) )
                  {
                     grp = String.Empty;
                  }
                  else
                  {
                     grp = Path.GetFileName( targetDir );
                     if ( String.IsNullOrEmpty( grp ) )
                     {
                        grp = targetDir;
                     }
                  }
                  return grp;
               } ).Select( g =>
               {
                  // Extract "MyAssembly.dll" from e.g. lib\net40\MyAssembly.dll into file attribute of <file> element
                  var grpX = new XElement( "group", g.Select( r => new XElement( "reference", new XAttribute( "file", Path.GetFileName( r ) ) ) ) );
                  if ( !String.IsNullOrEmpty( g.Key ) )
                  {
                     grpX.SetAttributeValue( "targetFramework", g.Key );
                  }

                  return grpX;
               } ) ) );
            }
         }
      }

      private void AddTargetFWAssemblies( XElement parent )
      {
         var refs = this.FrameworkAssemblies;
         if ( refs != null && refs.Length > 0 )
         {
            parent.Add( new XElement( "frameworkAssemblies", refs.Select( r =>
            {
               var refX = new XElement( "frameworkAssembly", new XAttribute( "assemblyName", r.ItemSpec ) );
               var tfw = r.GetMetadata( ITEM_MD_TARGET_FW );
               if ( !String.IsNullOrEmpty( tfw ) )
               {
                  refX.SetAttributeValue( "targetFramework", tfw );
               }
               return tfw;
            } ) ) );
         }
      }

      private void AddFiles( XElement parent )
      {
         var files = this.Files;
         if ( files != null && files.Length > 0 )
         {
            parent.Add( new XElement( "files", files.Select( file =>
            {
               var fileX = new XElement( "file", new XAttribute( "src", file.ItemSpec ), new XAttribute( "target", file.GetMetadata( ITEM_MD_TARGET_PATH ) ) );
               var exclude = file.GetMetadata( ITEM_MD_EXCLUDE );
               if ( !String.IsNullOrEmpty( exclude ) )
               {
                  fileX.SetAttributeValue( "exclude", exclude );
               }
               return fileX;
            } ) ) );
         }
      }
   }
}