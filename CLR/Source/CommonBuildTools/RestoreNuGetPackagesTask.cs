using CommonBuildTools;
using Microsoft.Build.Framework;
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
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CommonBuildTools
{
   public class RestoreNuGetPackagesTask : Task
   {
      public String NuGetManagementFile { get; set; }
      public String NuGetManagementContents { get; set; }
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
            try
            {
               retVal = this.ProcessNuGetManagement( nuGetManagementDoc.Root.CreateNuGetManagement() );
            }
            catch ( Exception e )
            {
               this.Log.LogError( "Failed to process NuGet management: {0}.", e.Message );
            }
         }

         return retVal;
      }

      private Boolean ProcessNuGetManagement( NuGetManagement ngm )
      {
         // Check which packages are actually missing
         var missingPackages = ngm.Packages
            .Where( pkg => !String.IsNullOrEmpty( pkg.PackageID ) && pkg.Versions.Count > 0 )
            .SelectMany( pkg =>
            {
               var pkgDir = ngm.GetPackagesDirectory( pkg.PackageSpecificConfiguration );
               var pkgID = pkg.PackageID;
               return pkg.Versions
                  .Where( version => !Directory.Exists( Path.Combine( pkgDir, pkgID + "." + version ) ) )
                  .Select( version =>
                  {
                     this.Log.LogMessage( MessageImportance.High, "Package {0} will be restored to directory {1}.", pkgID, pkgDir );
                     return Tuple.Create( pkgID, pkgDir, version );
                  } );
            } )
            .ToList();

         var retVal = true;
         if ( missingPackages.Count > 0 )
         {

            var pkgConfigFile = Path.GetTempFileName();
            try
            {
               // Create a packages.config file
               // <packages>
               //   <package id="Tuple.Item1" version="Tuple.Item3" />
               //   ...
               // </packages>
               new XDocument(
                  new XDeclaration( "1.0", "utf-8", "yes" ),
                  new XElement( "packages", missingPackages.Select( tuple =>
                     new XElement( "package", new XAttribute( "id", tuple.Item1 ), new XAttribute( "version", tuple.Item3 ) )
                     ) )
                  ).Save( pkgConfigFile );

               // Invoke NuGet
               // nuget restore <packages.config file> -Source -PackagesDirectory -ConfigFile
            }
            finally
            {
               try
               {
                  File.Delete( pkgConfigFile );
               }
               catch
               {
                  this.Log.LogMessage( MessageImportance.Normal, "Failed to delete temporary packages.config file: {0}.", pkgConfigFile );
               }
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
      public NuGetConfiguration GlobalConfiguration { get; set; }
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
      public NuGetConfiguration PackageSpecificConfiguration { get; set; }
   }

   internal class NuGetConfiguration
   {
      private readonly List<String> _sources;

      internal NuGetConfiguration( IEnumerable<String> sources = null )
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
   }
}

internal static partial class E_CBT
{
   public static NuGetManagement CreateNuGetManagement( this XElement element )
   {
      return element == null ? null : new NuGetManagement( element.ListOrEmpty( "Pacakges", "Package" ).Select( el => el.CreateNuGetPackage() ) )
      {
         GlobalConfiguration = element.Element( "GlobalConfiguration" ).CreateNuGetConfiguration()
      };
   }
   public static NuGetPackage CreateNuGetPackage( this XElement element )
   {
      return element == null ? null : new NuGetPackage( element.ListOrEmpty( "Versions", "Version" ).Select( el => el.Value ) )
      {
         PackageID = element.ValueOrNull( "ID" ),
         PackageSpecificConfiguration = element.Element( "PackageSpecificConfiguration" ).CreateNuGetConfiguration()
      };
   }

   public static NuGetConfiguration CreateNuGetConfiguration( this XElement element )
   {
      return element == null ? null : new NuGetConfiguration( element.ListOrEmpty( "Sources", "Source" ).Select( el => el.Value ) )
      {
         PackagesDirectory = element.ValueOrNull( "PackagesDirectory" )
      };
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

   public static String GetPackagesDirectory( this NuGetManagement ngm, NuGetConfiguration packageSpecificConfig )
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
}
