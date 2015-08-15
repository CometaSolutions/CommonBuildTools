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
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using CommonBuildTools;
using System.Xml.Linq;
using System.Xml.XPath;

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

   public abstract class AbstractNuGetTask : ToolTask
   {
      public String NuGetExecutable { get; set; }

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

      protected override abstract String GenerateCommandLineCommands();
   }

   public class CallNuGetExecutableTask : AbstractNuGetTask
   {
      public String NuGetArguments { get; set; }

      protected override String GenerateCommandLineCommands()
      {
         return this.NuGetArguments;
      }
   }

   public class NuGetRestoreTask : AbstractNuGetTask
   {
      public String NuGetManagementFile { get; set; }
      public String NuGetManagementContents { get; set; }

      private NuGetManagement _nugetManagement;
      private PackagesFileInfo? _currentPackage;

      protected override String GenerateCommandLineCommands()
      {
         var ngm = this._nugetManagement;
         var pkgNullable = this._currentPackage;
         if ( ngm != null && pkgNullable.HasValue )
         {
            var pkg = pkgNullable.Value;
            return ngm.CreateNuGetParameters( pkg );
         }
         else
         {
            this.Log.LogError( "Internal error, no info to generate NuGet command line arguments from." );
            throw new Exception();
         }
      }

      public override Boolean Execute()
      {
         var retVal = false;
         // First, deduce how many packages.config files we need to create
         var infos = this.CreatePackagesFileInfos( out this._nugetManagement );
         if ( infos != null )
         {
            // Then execute NuGet restore for each packages.config file
            var seenError = false;

            if ( infos.Count > 0 )
            {
               try
               {
                  if ( this.CreatePackageConfigFiles( infos ) )
                  {
                     foreach ( var info in infos )
                     {
                        this._currentPackage = info;
                        try
                        {
                           if ( !base.Execute() )
                           {
                              seenError = true;
                           }
                        }
                        catch
                        {
                           seenError = true;
                        }
                     }
                  }
                  else
                  {
                     seenError = true;
                  }
               }
               finally
               {
                  foreach ( var info in infos )
                  {
                     var file = info.PackagesConfigPath;
                     if ( File.Exists( file ) )
                     {
                        try
                        {
                           File.Delete( file );
                           Directory.Delete( Path.GetDirectoryName( file ) );
                        }
                        catch ( Exception exc )
                        {
                           this.Log.LogMessage( MessageImportance.Normal, "Failed to delete generated packages.config file {0}: {1}.", file, exc.Message );
                        }
                     }
                  }
               }
            }
            retVal = !seenError;
         }

         return retVal;

      }

      private List<PackagesFileInfo> CreatePackagesFileInfos( out NuGetManagement ngm )
      {
         var nugetManagementFile = this.NuGetManagementFile;
         XDocument nuGetManagementDoc = null;

         String ngmLocation;
         if ( String.IsNullOrEmpty( nugetManagementFile ) )
         {
            ngmLocation = Environment.CurrentDirectory;
            var nugetManagementContents = this.NuGetManagementContents;
            if ( String.IsNullOrEmpty( nugetManagementContents ) )
            {
               this.Log.LogError( "Either management file path or file contents must be specified." );
            }
            else
            {
               try
               {
                  nuGetManagementDoc = XDocument.Parse( nugetManagementContents );
               }
               catch ( Exception e )
               {
                  this.Log.LogError( "Failed to parse management file contents: {0}.", e.Message );
               }
            }
         }
         else
         {
            ngmLocation = Path.GetDirectoryName( nugetManagementFile );
            try
            {
               nuGetManagementDoc = XDocument.Load( nugetManagementFile );
            }
            catch ( Exception e )
            {
               this.Log.LogError( "Failed to load NuGet management configuration from {0}: {1}.", nugetManagementFile, e.Message );
            }
         }

         List<PackagesFileInfo> packageInfos = null;
         ngm = null;
         if ( nuGetManagementDoc != null )
         {
            try
            {
               ngm = nuGetManagementDoc
                  .XPathSelectElement( "//NuGetManagement" )
                  .CreateNuGetManagement( ngmLocation );
               if ( ngm != null )
               {
                  packageInfos = this.ExtractPackagesFileInfos( ngm );
               }
            }
            catch ( Exception e )
            {
               this.Log.LogError( "Failed to process NuGet management: {0}.", e.Message );
            }
         }

         return packageInfos;
      }

      private List<PackagesFileInfo> ExtractPackagesFileInfos( NuGetManagement ngm )
      {

         return ngm.Packages
            .Where( pkg => !String.IsNullOrEmpty( pkg.PackageID ) && pkg.Versions.Count > 0 )
            .SelectMany( pkg =>
            {
               // Check which packages are actually missing
               var pkgDir = ngm.GetPackagesDirectory( pkg.PackageSpecificConfiguration );
               var pkgID = pkg.PackageID;
               return pkg.Versions
                  .Where( version => !String.IsNullOrEmpty( version ) && !Directory.Exists( Path.Combine( pkgDir, pkgID + "." + version ) ) )
                  .Select( version =>
                  {
                     //this.Log.LogMessage( MessageImportance.High, "Package {0} will be restored to directory {1}.", pkgID, pkgDir );
                     return Tuple.Create( pkg, pkgID, pkgDir, version );
                  } );
            } )
            .GroupBy( tuple => Tuple.Create(
               tuple.Item3,
               ngm.GetConfigurationFile( tuple.Item1.PackageSpecificConfiguration ),
               ngm.GetNoCache( tuple.Item1.PackageSpecificConfiguration )
               ) ) // Key is tuple of package directory, configuration file, and no-cache variable, as these are specified per-restore
            .Select( g =>
            {
               var key = g.Key;
               var info = new PackagesFileInfo(
                  key.Item1,
                  Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString(), "packages.config" ),
                  key.Item2,
                  key.Item3
                  );
               info.Sources.AddRange( g.SelectMany( tuple => ngm.GetSources( tuple.Item1.PackageSpecificConfiguration ) ) );
               info.Packages.AddRange( g.Select( tuple => new PackageInfo( tuple.Item2, tuple.Item4 ) ) );
               return info;
            } )
            .ToList();
      }

      private Boolean CreatePackageConfigFiles( IEnumerable<PackagesFileInfo> packageConfigs )
      {
         var retVal = true;
         foreach ( var cfg in packageConfigs )
         {
            var curFile = cfg.PackagesConfigPath;
            this.Log.LogMessage( MessageImportance.High, "Storing packages {0} to {1}.", String.Join( ", ", cfg.Packages.Select( pkg => pkg.PackageID + ":" + pkg.PackageVersion ) ), curFile );

            try
            {
               var dir = Path.GetDirectoryName( curFile );
               if ( !Directory.Exists( dir ) )
               {
                  Directory.CreateDirectory( dir );
               }
               new XDocument(
                  new XDeclaration( "1.0", "utf-8", "yes" ),
                  new XElement( "packages", cfg.Packages.Select( pkg =>
                     new XElement( "package", new XAttribute( "id", pkg.PackageID ), new XAttribute( "version", pkg.PackageVersion ) )
                     ) )
                  ).Save( curFile );
            }
            catch ( Exception exc )
            {
               this.Log.LogError( "Error while saving packages.config file to {0}: {1}.", curFile, exc.Message );
               retVal = false;
               break;
            }
         }

         return retVal;
      }
   }


   internal class NuGetManagement
   {
      private readonly List<NuGetPackage> _packages;

      internal NuGetManagement( IEnumerable<NuGetPackage> packages = null )
      {
         this._packages = packages == null ? new List<NuGetPackage>() : packages.ToList();
      }

      public List<NuGetPackage> Packages
      {
         get
         {
            return this._packages;
         }
      }
      public GlobalNuGetConfiguration GlobalConfiguration { get; set; }
   }

   internal class NuGetPackage
   {
      private readonly List<String> _versions;

      internal NuGetPackage( IEnumerable<String> versions = null )
      {
         this._versions = versions == null ? new List<String>() : versions.ToList();
      }

      public String PackageID { get; set; }
      public List<String> Versions
      {
         get
         {
            return this._versions;
         }
      }
      public PackageSpecificConfiguration PackageSpecificConfiguration { get; set; }
   }

   internal abstract class AbstractNuGetConfiguration
   {
      private readonly List<String> _sources;

      internal AbstractNuGetConfiguration( IEnumerable<String> sources )
      {
         this._sources = sources == null ? new List<String>() : sources.ToList();
      }

      public List<String> Sources
      {
         get
         {
            return this._sources;
         }
      }
      public String PackagesDirectory { get; set; }
      public String ConfigurationFile { get; set; }
      public Boolean? NoCache { get; set; }
   }

   internal sealed class GlobalNuGetConfiguration : AbstractNuGetConfiguration
   {
      internal GlobalNuGetConfiguration( IEnumerable<String> sources )
         : base( sources )
      {

      }

      public Boolean DisableParallelProcessing { get; set; }
   }

   internal sealed class PackageSpecificConfiguration : AbstractNuGetConfiguration
   {
      internal PackageSpecificConfiguration( IEnumerable<String> sources )
         : base( sources )
      {

      }
   }

   internal struct PackagesFileInfo
   {
      private readonly String _packagesDirPath;
      private readonly String _packagesConfigFilePath;
      private readonly String _nuGetConfigFilePath;
      private readonly Boolean _noCache;
      private readonly List<String> _sources;
      private readonly List<PackageInfo> _packages;

      internal PackagesFileInfo(
         String packagesDir,
         String packagesConfig,
         String nuGetConfig,
         Boolean noCache
         )
      {
         this._packagesDirPath = packagesDir;
         this._packagesConfigFilePath = packagesConfig;
         this._nuGetConfigFilePath = nuGetConfig;
         this._noCache = noCache;
         this._sources = new List<String>();
         this._packages = new List<PackageInfo>();
      }

      public String OutputDirectory
      {
         get
         {
            return this._packagesDirPath;
         }
      }

      public String PackagesConfigPath
      {
         get
         {
            return this._packagesConfigFilePath;
         }
      }

      public String NuGetConfigPath
      {
         get
         {
            return this._nuGetConfigFilePath;
         }
      }

      public Boolean NoCache
      {
         get
         {
            return this._noCache;
         }
      }

      public List<String> Sources
      {
         get
         {
            return this._sources;
         }
      }

      public List<PackageInfo> Packages
      {
         get
         {
            return this._packages;
         }
      }
   }

   internal struct PackageInfo
   {
      private readonly String _packageID;
      private readonly String _packageVersion;

      internal PackageInfo( String packageID, String packageVersion )
      {
         this._packageID = packageID;
         this._packageVersion = packageVersion;
      }

      public String PackageID
      {
         get
         {
            return this._packageID;
         }
      }

      public String PackageVersion
      {
         get
         {
            return this._packageVersion;
         }
      }
   }

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

      [Output]
      public String GeneratedNuSpecFilePath { get; set; }

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
            var nuspec = new XElement( "package", md );

            // Files
            this.AddFiles( nuspec );

            // Change namespace of all elements and attributes
            XNamespace ns = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd";
            foreach ( var el in nuspec.XPathSelectElements( "//*" ).ToArray() )
            {
               el.Name = ns + el.Name.LocalName;
               foreach ( var attr in el.Attributes().ToArray() )
               {
                  el.SetAttributeValue( attr.Name, null );
                  el.SetAttributeValue( ns + attr.Name.LocalName, attr.Value );
               }
            }

            // Save
            var outDir = Path.GetDirectoryName( outputPath );
            if ( !Directory.Exists( outDir ) )
            {
               Directory.CreateDirectory( outDir );
            }

            new XDocument( new XDeclaration( "1.0", "utf-8", "yes" ), nuspec )
               .Save( outputPath );

            this.GeneratedNuSpecFilePath = outputPath;
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
            // .ToString will return 'True' or 'False', which will violate case-sensitive NuGet XSD schema rules
            AddElement( parent, elementName, content.Value ? "true" : "false" );
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

   public class GenerateNuGetPackageFileTask : AbstractNuGetTask
   {

      [Required]
      public String NuSpecFile { get; set; }

      public String OutputDirectory { get; set; }

      public String BasePath { get; set; }

      public String Version { get; set; }

      public String MinClientVersion { get; set; }

      public Boolean NoDefaultExcludes { get; set; }

      public Boolean NoPackageAnalysis { get; set; }

      public Boolean ExcludeEmptyDirectories { get; set; }

      protected override String GenerateCommandLineCommands()
      {
         var builder = new CommandLineBuilder();
         builder.AppendTextUnquoted( "pack " );

         builder.AppendFileNameIfNotNull( this.NuSpecFile );

         builder.AppendSwitchIfNotNull( "-OutputDirectory ", this.OutputDirectory );

         builder.AppendSwitchIfNotNull( "-BasePath ", this.BasePath );

         builder.AppendSwitchIfNotNull( "-Version ", this.Version );

         builder.AppendSwitchIfNotNull( "-MinClientVersion ", this.MinClientVersion );

         if ( this.NoDefaultExcludes )
         {
            builder.AppendSwitch( "-NoDefaultExcludes " );
         }

         if ( this.NoPackageAnalysis )
         {
            builder.AppendSwitch( "-NoPackageAnalysis " );
         }

         if ( this.ExcludeEmptyDirectories )
         {
            builder.AppendSwitch( "-ExcludeEmptyDirectories " );
         }

         builder.AppendTextUnquoted( " -Verbosity detailed -NonInteractive" );

         return builder.ToString();
      }
   }
}

internal static partial class E_CBT
{
   public static NuGetManagement CreateNuGetManagement( this XElement element, String ngmLocation )
   {
      return element == null ?
         null :
         new NuGetManagement( element.ListOrEmpty( "Packages", "Package" ).Select( el => el.CreateNuGetPackage( ngmLocation ) ) )
         {
            GlobalConfiguration = (GlobalNuGetConfiguration) element.Element( "GlobalConfiguration" ).CreateNuGetConfiguration( true, ngmLocation )
         };
   }
   public static NuGetPackage CreateNuGetPackage( this XElement element, String ngmLocation )
   {
      return element == null ?
         null :
         new NuGetPackage( element.ListOrEmpty( "Versions", "Version" ).Select( el => el.Value ) )
         {
            PackageID = element.ValueOrNull( "ID" ),
            PackageSpecificConfiguration = (PackageSpecificConfiguration) element.Element( "PackageSpecificConfiguration" ).CreateNuGetConfiguration( false, ngmLocation )
         };
   }

   public static AbstractNuGetConfiguration CreateNuGetConfiguration( this XElement element, Boolean isGlobal, String ngmLocation )
   {
      AbstractNuGetConfiguration retVal;
      if ( element == null )
      {
         retVal = null;
      }
      else
      {
         var sources = element.ListOrEmpty( "Sources", "Source" ).Select( el => el.Value );
         retVal = isGlobal ?
            (AbstractNuGetConfiguration) new GlobalNuGetConfiguration( sources )
            {
               DisableParallelProcessing = element.ValueOrNull( "DisableParallelProcessing" ).ParseAsBooleanSafe()
            } :
            new PackageSpecificConfiguration( sources );

         element.InitializeAbstractNuGetConfiguration( retVal, ngmLocation );
      }

      return retVal;
   }

   private static void InitializeAbstractNuGetConfiguration( this XElement element, AbstractNuGetConfiguration config, String ngmLocation )
   {
      if ( element != null && config != null )
      {
         config.PackagesDirectory = element.ValueOrNull( "PackagesDirectory" ).ConvertRelativeToAbsolute( ngmLocation );
         config.ConfigurationFile = element.ValueOrNull( "ConfigurationFile" ).ConvertRelativeToAbsolute( ngmLocation );
         var noCacheElement = element.ValueOrNull( "NoCache" );
         Boolean? noCache;
         if ( noCacheElement == null )
         {
            noCache = null;
         }
         else
         {
            noCache = noCacheElement.ParseAsBooleanSafe();
         }
         config.NoCache = noCache;
      }
   }

   private static String ValueOrNull( this XElement element, String containerName )
   {
      var container = element == null ? null : element.Element( containerName );
      return container == null ? null : container.Value;
   }

   private static IEnumerable<XElement> ListOrEmpty( this XElement element, String listContainerName, String listItemName )
   {
      var container = element == null ? null : element.Element( listContainerName );

      if ( container != null )
      {
         foreach ( var item in container.Elements( listItemName ) )
         {
            yield return item;
         }
      }
   }

   public static String GetPackagesDirectory( this NuGetManagement ngm, PackageSpecificConfiguration packageSpecificConfig )
   {
      String retVal = null;
      if ( packageSpecificConfig != null )
      {
         retVal = packageSpecificConfig.PackagesDirectory;
      }

      if ( String.IsNullOrEmpty( retVal ) && ngm.GlobalConfiguration != null )
      {
         retVal = ngm.GlobalConfiguration.PackagesDirectory;
      }

      if ( String.IsNullOrEmpty( retVal ) )
      {
         retVal = Environment.CurrentDirectory;
      }

      return Path.GetFullPath( retVal );
   }

   public static IEnumerable<String> GetSources( this NuGetManagement ngm, PackageSpecificConfiguration packageSpecificConfig )
   {
      if ( packageSpecificConfig != null )
      {
         foreach ( var src in packageSpecificConfig.Sources )
         {
            yield return src;
         }
      }

      if ( ngm.GlobalConfiguration != null )
      {
         foreach ( var src in ngm.GlobalConfiguration.Sources )
         {
            yield return src;
         }
      }
   }

   public static String GetConfigurationFile( this NuGetManagement ngm, PackageSpecificConfiguration packageSpecificConfig )
   {
      String retVal = null;
      if ( packageSpecificConfig != null )
      {
         retVal = packageSpecificConfig.ConfigurationFile;
      }

      if ( String.IsNullOrEmpty( retVal ) && ngm.GlobalConfiguration != null )
      {
         retVal = ngm.GlobalConfiguration.ConfigurationFile;
      }

      return retVal;
   }

   public static Boolean GetNoCache( this NuGetManagement ngm, PackageSpecificConfiguration packageSpecificConfig )
   {
      Boolean? retVal = null;
      if ( packageSpecificConfig != null )
      {
         retVal = packageSpecificConfig.NoCache;
      }

      if ( !retVal.HasValue && ngm.GlobalConfiguration != null )
      {
         retVal = ngm.GlobalConfiguration.NoCache;
      }

      return retVal.HasValue && retVal.Value;
   }

   internal static String CreateNuGetParameters( this NuGetManagement ngm, PackagesFileInfo info )
   {
      var cmd = new CommandLineBuilder();
      cmd.AppendTextUnquoted( "restore " );

      cmd.AppendFileNameIfNotNull( info.PackagesConfigPath );

      cmd.AppendSwitchIfNotNull( "-PackagesDirectory ", info.OutputDirectory );

      cmd.AppendSwitchIfNotNull( "-Source ", info.Sources.ToArray().NullIfEmpty(), ";" );

      if ( info.NoCache )
      {
         cmd.AppendSwitch( "-NoCache " );
      }

      cmd.AppendSwitchIfNotNull( "-ConfigFile ", info.NuGetConfigPath.NullIfEmpty() );

      if ( ngm.GlobalConfiguration != null && ngm.GlobalConfiguration.DisableParallelProcessing )
      {
         cmd.AppendSwitch( "-DisableParallelProcsessing " );
      }

      cmd.AppendTextUnquoted( " -Verbosity detailed -NonInteractive" );

      return cmd.ToString();
   }

   internal static Boolean ParseAsBooleanSafe( this String str )
   {
      Boolean parsedBoolean;
      return Boolean.TryParse( str, out parsedBoolean ) && parsedBoolean;
   }

   internal static String NullIfEmpty( this String str )
   {
      return str != null && str.Length == 0 ? null : str;
   }

   internal static T[] NullIfEmpty<T>( this T[] array )
   {
      return array != null && array.Length == 0 ? null : array;
   }

   internal static String ConvertRelativeToAbsolute( this String str, String ngmLocation )
   {
      if ( !String.IsNullOrEmpty( str ) && !Path.IsPathRooted( str ) )
      {
         str = Path.Combine( ngmLocation, str );
      }

      return str;
   }
}