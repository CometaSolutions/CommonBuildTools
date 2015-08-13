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
using CommonBuildTools;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CommonBuildTools
{
   public class CreateNuGetPackagesFilesTask : Task
   {


      public String NuGetManagementFile { get; set; }
      public String NuGetManagementContents { get; set; }

      [Output]
      public ITaskItem[] NuGetPackagesConfigFiles { get; set; }

      public override Boolean Execute()
      {
         var retVal = false;
         var file = this.NuGetManagementFile;

         XDocument nuGetManagementDoc = null;

         if ( String.IsNullOrEmpty( file ) )
         {
            var contens = this.NuGetManagementContents;
            if ( String.IsNullOrEmpty( contens ) )
            {
               this.Log.LogError( "Either management file path or file contents must be specified." );
            }
            else
            {
               try
               {
                  nuGetManagementDoc = XDocument.Parse( contens );
               }
               catch ( Exception e )
               {
                  this.Log.LogError( "Failed to parse management file contents: {0}.", e.Message );
               }
            }
         }
         else
         {
            try
            {
               nuGetManagementDoc = XDocument.Load( file );
            }
            catch ( Exception e )
            {
               this.Log.LogError( "Failed to load NuGet management configuration from {0}: {1}.", file, e.Message );
            }
         }

         if ( nuGetManagementDoc != null )
         {
            List<PackagesFileInfo> packageInfos = null;
            try
            {
               var ngm = nuGetManagementDoc.Root.CreateNuGetManagement();
               packageInfos = this.ExtractPackagesFileInfos( ngm );

               this.NuGetPackagesConfigFiles = packageInfos
                  .Select( pkg => ngm.CreateTaskItem( pkg ) )
                  .ToArray();
               this.CreatePackageConfigFiles( packageInfos );

               retVal = true;
            }
            catch ( Exception e )
            {
               this.Log.LogError( "Failed to process NuGet management: {0}.", e.Message );
            }

            if ( !retVal && packageInfos != null )
            {
               // Clean-up generated files
               foreach ( var fn in packageInfos.Select( pkg => pkg.PackagesConfigPath ) )
               {
                  if ( File.Exists( fn ) )
                  {
                     try
                     {
                        File.Delete( fn );
                     }
                     catch
                     {
                        // Ignore
                     }
                  }
               }
            }
         }

         return retVal;
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
                  Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString() + ".config" ),
                  key.Item2,
                  key.Item3
                  );
               info.Sources.AddRange( g.SelectMany( tuple => ngm.GetSources( tuple.Item1.PackageSpecificConfiguration ) ) );
               info.Packages.AddRange( g.Select( tuple => new PackageInfo( tuple.Item2, tuple.Item4 ) ) );
               return info;
            } )
            .ToList();
      }

      private void CreatePackageConfigFiles( IEnumerable<PackagesFileInfo> packageConfigs )
      {
         foreach ( var cfg in packageConfigs )
         {
            new XDocument(
               new XDeclaration( "1.0", "utf-8", "yes" ),
               new XElement( "packages", cfg.Packages.Select( pkg =>
                  new XElement( "package", new XAttribute( "id", pkg.PackageID ), new XAttribute( "version", pkg.PackageVersion ) )
                  ) )
               ).Save( cfg.PackagesConfigPath );
         }
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
}

internal static partial class E_CBT
{
   public static NuGetManagement CreateNuGetManagement( this XElement element )
   {
      return element == null ?
         null :
         new NuGetManagement( element.ListOrEmpty( "Pacakges", "Package" ).Select( el => el.CreateNuGetPackage() ) )
         {
            GlobalConfiguration = (GlobalNuGetConfiguration) element.Element( "GlobalConfiguration" ).CreateNuGetConfiguration( true )
         };
   }
   public static NuGetPackage CreateNuGetPackage( this XElement element )
   {
      return element == null ?
         null :
         new NuGetPackage( element.ListOrEmpty( "Versions", "Version" ).Select( el => el.Value ) )
         {
            PackageID = element.ValueOrNull( "ID" ),
            PackageSpecificConfiguration = (PackageSpecificConfiguration) element.Element( "PackageSpecificConfiguration" ).CreateNuGetConfiguration( false )
         };
   }

   public static AbstractNuGetConfiguration CreateNuGetConfiguration( this XElement element, Boolean isGlobal )
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

         element.InitializeAbstractNuGetConfiguration( retVal );
      }

      return retVal;
   }

   private static void InitializeAbstractNuGetConfiguration( this XElement element, AbstractNuGetConfiguration config )
   {
      if ( element != null && config != null )
      {
         config.PackagesDirectory = element.ValueOrNull( "PackagesDirectory" );
         config.ConfigurationFile = element.ValueOrNull( "ConfigurationFile" );
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
      return container == null ? null : element.Value;
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

   internal static ITaskItem CreateTaskItem( this NuGetManagement ngm, PackagesFileInfo info )
   {
      var sb = new StringBuilder();
      sb.Append( "\"" )
         .Append( info.PackagesConfigPath )
         .Append( "\" -PackagesDirectory \"" )
         .Append( info.OutputDirectory )
         .Append( "\" " );

      if ( info.Sources.Count > 0 )
      {
         sb.Append( "-Source \"" )
            .Append( String.Join( ";", info.Sources ) )
            .Append( "\" " );
      }

      if ( info.NoCache )
      {
         sb.Append( "-NoCache " );
      }

      if ( !String.IsNullOrEmpty( info.NuGetConfigPath ) )
      {
         sb.Append( "-ConfigFile \"" )
            .Append( info.NuGetConfigPath )
            .Append( "\" " );
      }

      if ( ngm.GlobalConfiguration != null && ngm.GlobalConfiguration.DisableParallelProcessing )
      {
         sb.Append( "-DisableParallelProcsessing " );
      }

      sb.Append( "-Verbosity detailed -NonInteractive" );

      var retVal = new TaskItem( info.PackagesConfigPath );
      retVal.SetMetadata( "NuGetParameters", sb.ToString() );
      return retVal;
   }

   private static Boolean ParseAsBooleanSafe( this String str )
   {
      Boolean parsedBoolean;
      return Boolean.TryParse( str, out parsedBoolean ) && parsedBoolean;
   }
}
