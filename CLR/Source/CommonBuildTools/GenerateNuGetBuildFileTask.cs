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
using Microsoft.Build.Framework;
using System.IO;

namespace CommonBuildTools
{
   public class GenerateNuGetBuildFileTask : Task
   {
      private const String TEMPLATE =
"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
"<Project\n" +
"  DefaultTargets=\"{0}_CheckVariables;{0}_Tests;{0}_Compile;{0}_PEVerify;{0}_NuGet\"\n" +
"  xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"\n" +
"  >\n" +
"  <PropertyGroup>\n" +
"    <{0}BaseDir Condition=\" '$({0}BaseDir)' == '' \">{1}</{0}BaseDir>\n" +
"  </PropertyGroup>\n" +
"  \n" +
"  <Target Name=\"{0}_CheckVariables\">\n" +
"    <!-- Must specify release notes -->\n" +
"    <Error Condition=\"'$({0}ReleaseNotes)' == ''\" Text=\"Please specify release notes in {0}ReleaseNotes property.\" />\n" +
"  </Target>\n" +
"  \n" +
"  <Target Name=\"{0}_Tests\">\n" +
"    <!-- NuGet restore (NUnit package) -->\n" +
"    <CommonBuildTools.NuGetTaskRestore\n" +
"      NuGetExecutable=\"$({0}NuGetExecutable)\"\n" +
"      NuGetManagementFile=\"$({0}BaseDir)/{2}\"\n" +
"      />\n" +
"      \n" +
"    <!-- Compile CIL Tests assembly -->\n" +
"    <MSBuild\n" +
"      Projects=\"$({0}BaseDir)/{3}\"\n" +
"      Properties=\"Configuration=Release\"\n" +
"      />\n" +
"      \n" +
"    <!-- Call NUnit task -->\n" +
"    <CommonBuildTools.NUnitTask\n" +
"      Assemblies=\"{5}\"\n" +
"      NoShadowAssemblies=\"True\"\n" +
"      WorkingDirectory=\"$({0}BaseDir)/{4}\"\n" +
"      />\n" +
"  </Target>\n" +
"  \n" +
"  <Target Name=\"{0}_Compile\">   \n" +
"    <MSBuild Projects=\"$(MSBuildThisFileDirectory){0}.build\" Properties=\"{0}Configuration=Release\" />\n" +
"  </Target>\n" +
"   \n" +
"  <Target Name=\"{0}_PEVerify\">\n" +
"    <!-- First, delete all files that won't be included in the NuGet package. -->\n" +
"    <ItemGroup>\n" +
"      <{0}FilesToPersist Include=\"$({0}BaseDir)/{6}/{23}.*\"/>\n" +
"      \n" +
"      <{0}FilesToDelete Include=\"$({0}BaseDir)/{6}/*.*\"/>\n" +
"      <{0}FilesToDelete Remove=\"@({0}FilesToPersist)\"/>\n" +
"    </ItemGroup>\n" +
"    <Delete\n" +
"      Files=\"@({0}FilesToDelete)\"\n" +
"    />\n" +
"    \n" +
"    <!-- Files for PEVerify -->\n" +
"    <ItemGroup>\n" +
"      <PEVerifyFiles Include=\"$({0}BaseDir)/{6}/{23}.dll\" />\n" +
"    </ItemGroup>\n" +
"    \n" +
"    <!-- Verify all .dll files exist -->\n" +
"    <PropertyGroup>\n" +
"      <PEVerifyFilesCount>@(PEVerifyFiles->Count())</PEVerifyFilesCount>\n" +
"      <PEVerifyFilesExpectedCount>1</PEVerifyFilesExpectedCount>\n" +
"    </PropertyGroup>\n" +
"    <Error Condition=\" '$(PEVerifyFilesCount)' != '$(PEVerifyFilesExpectedCount)' \" Text=\"Not all required files for PEVerify are present ($(PEVerifyFilesCount)).\" />\n" +
"\n" +
"    <!-- Call PEVerify -->\n" +
"    <CommonBuildTools.PEVerifyTask\n" +
"      FileToVerify=\"%(PEVerifyFiles.Identity)\"\n" +
"      />\n" +
"  </Target>\n" +
"   \n" +
"  <Target Name=\"{0}_NuGet\">\n" +
"    <!-- NuSpec file information -->\n" +
"    <PropertyGroup>\n" +
"      <!-- Common -->\n" +
"      <{0}BaseDirNuGet>$({0}BaseDir)/{7}</{0}BaseDirNuGet>\n" +
"    \n" +
"      <!-- NuGet Spec -->\n" +
"      <{0}NuSpecVersionFilename Condition=\" '$({0}NuSpecVersion)' == '' \">$({0}BaseDir)/{8}</{0}NuSpecVersionFilename>\n" +
"      <{0}NuSpecFilePath>$({0}BaseDirNuGet)/{24}.nuspec</{0}NuSpecFilePath>\n" +
"    </PropertyGroup>\n" +
"    <ItemGroup>\n" +
"      <{0}NuGetFile Include=\"{6}/{23}.dll\">\n" +
"        <TargetFilePath>{9}/{23}.dll</TargetFilePath>\n" +
"      </{0}NuGetFile>\n" +
"    </ItemGroup>\n" +
"    \n" +
"    <!-- Generate .nuspec file -->\n" +
"    <CommonBuildTools.NuGetTaskNuSpec\n" +
"      VersionFile=\"$({0}NuSpecVersionFilename)\"\n" +
"      VersionContents=\"$({0}NuSpecVersion)\"\n" +
"      Copyright_InceptionYear=\"{10}\"\n" +
"      PackageID=\"{24}\"\n" +
"      Authors=\"{11}\"\n" +
"      Description=\"{12}\"\n" +
"      Title=\"{13}\"\n" +
"      ReleaseNotes=\"$({0}ReleaseNotes)\"\n" +
"      Tags=\"{14}\"\n" +
"      Summary=\"{15}\"\n" +
"      ProjectURL=\"{16}\"\n" +
"      LicenseURL=\"{17}\"\n" +
"      IconURL=\"{18}\"\n" +
"      RequireLicenseAcceptance=\"{19}\"\n" +
"      Files=\"@({0}NuGetFile)\"\n" +
"      OutputPath=\"$({0}NuSpecFilePath)\"\n" +
"      >\n" +
"      <Output TaskParameter=\"GeneratedNuSpecVersion\" PropertyName=\"{0}NuSpecVersionGenerated\" />\n" +
"    </CommonBuildTools.NuGetTaskNuSpec>\n" +
"\n" +
"    <!-- Generate the .nupkg file -->\n" +
"    <CommonBuildTools.NuGetTaskPackage\n" +
"      NuSpecFile=\"$({0}NuSpecFilePath)\"\n" +
"      OutputDirectory=\"$({0}BaseDir)/{20}\"\n" +
"      BasePath=\"$({0}BaseDir)\"\n" +
"      MinClientVersion=\"{21}\"\n" +
"    />\n" +
"    \n" +
"    <!-- Push if API-key or config file property specified -->\n" +
"    <CommonBuildTools.NuGetTaskPush\n" +
"      Condition=\" '$({0}NuGetPushAPIKey)' != '' or '$({0}NuGetPushConfigFile)' != '' \"\n" +
"      PackageFilePath=\"$({0}BaseDirNuGet)/{0}.$({0}NuSpecVersionGenerated).nupkg\"\n" +
"      APIKey=\"$({0}NuGetPushAPIKey)\"\n" +
"      Source=\"$({0}NuGetPushSource)\"\n" +
"      ConfigFile=\"$({0}NuGetPushConfigFile)\"\n" +
"      />\n" +
"  </Target>\n" +
"  \n" +
"  <Import Project=\"$({0}BaseDir)/{22}/CLR/MSBuild/NuGetTasks.targets\" />\n" +
"  \n" +
"  <Import Project=\"$({0}BaseDir)/{22}/CLR/MSBuild/PEVerify.targets\" />\n" +
"    \n" +
"  <Import Project=\"$({0}BaseDir)/{22}/CLR/MSBuild/NUnit.targets\" />\n" +
"  \n" +
"</Project>";

      [Required]
      public String PackageID { get; set; }
      public String BuildFileLocation { get; set; }

      public Boolean NoDotReplacementInPackageID { get; set; }

      public String PathToVSCRoot { get; set; }
      public String PathToNuGetPackagesManagement { get; set; }
      public String PathToTestsProject { get; set; }
      public String PathToTestsAssembly { get; set; }
      public String PathToOutputDirectory { get; set; }
      public String PathToNuSpecDirectory { get; set; }
      public String PathToAssemblyVersionFile { get; set; }
      public String PathToNuGetPackageDirectory { get; set; }
      public String PathToCommonBuildTools { get; set; }
      public String OutputAssemblyName { get; set; }
      public String NuGetTargetDirectory { get; set; }
      public String NuGetInceptionYear { get; set; }
      public String NuGetAuthors { get; set; }
      public String NuGetDescription { get; set; }
      public String NuGetTitle { get; set; }
      public String NuGetTags { get; set; }
      public String NuGetSummary { get; set; }
      public String NuGetProjectURL { get; set; }
      public String NuGetLicenseURL { get; set; }
      public String NuGetIconURL { get; set; }
      public String NuGetRequireLicenseAcceptance { get; set; }
      public String NuGetMinClientVersion { get; set; }



      public override Boolean Execute()
      {
         var pid = this.PackageID;
         var retVal = false;
         if ( String.IsNullOrEmpty( pid ) )
         {
            this.Log.LogError( "Package ID was null or empty." );
         }
         else
         {
            var originalPID = pid;
            if ( !this.NoDotReplacementInPackageID )
            {
               pid = pid.Replace( ".", "" );
            }

            var testsAssembly = this.PathToTestsAssembly;
            String testsAssemblyDir, testsAssemblyName;
            if ( !String.IsNullOrEmpty( testsAssembly ) )
            {
               testsAssemblyDir = Path.GetDirectoryName( testsAssembly );
               testsAssemblyName = Path.GetFileName( testsAssembly );
            }
            else
            {
               testsAssemblyDir = "[TESTS_ASSEMBLY_DIRECTORY]";
               testsAssemblyName = "[TESTS_ASSEMBLY_NAME]";
            }
            var buildFileContents = String.Format( TEMPLATE,
               pid,
               this.PathToVSCRoot.ValueOrDefault( "[PATH_TO_VSC_ROOT]" ),
               this.PathToNuGetPackagesManagement.ValueOrDefault( "[PATH_TO_NUGET_PACKAGES_MANAGEMENT]" ),
               this.PathToTestsProject.ValueOrDefault( "[PATH_TO_TESTS_PROJECT]" ),
               testsAssemblyDir,
               testsAssemblyName,
               this.PathToOutputDirectory.ValueOrDefault( "[PATH_TO_OUTPUT_DIRECTORY]" ),
               this.PathToNuSpecDirectory.ValueOrDefault( "[PATH_TO_NUSPEC_DIRECTORY]" ),
               this.PathToAssemblyVersionFile.ValueOrDefault( "[PATH_TO_VERSION_FILE]" ),
               this.NuGetTargetDirectory.ValueOrDefault( "[NUGET_TARGET_PATH]" ),
               this.NuGetInceptionYear.ValueOrDefault( "[NUGET_INCEPTION_YEAR]" ),
               this.NuGetAuthors.ValueOrDefault( "[NUGET_AUTHORS]" ),
               this.NuGetDescription.ValueOrDefault( "[NUGET_DESCRIPTION]" ),
               this.NuGetTitle.ValueOrDefault( "[NUGET_TITLE]" ),
               this.NuGetTags.ValueOrDefault( "[NUGET_TAGS]" ),
               this.NuGetSummary.ValueOrDefault( "[NUGET_SUMMARY]" ),
               this.NuGetProjectURL.ValueOrDefault( "[NUGET_PROJECT_URL]" ),
               this.NuGetLicenseURL.ValueOrDefault( "[NUGET_LICENSE_URL]" ),
               this.NuGetIconURL.ValueOrDefault( "[NUGET_ICON_URL]" ),
               this.NuGetRequireLicenseAcceptance.ValueOrDefault( "[NUGET_REQUIRE_LICENSE_ACCEPTANCE]" ),
               this.PathToNuGetPackageDirectory.ValueOrDefault( "[PATH_TO_NUGET_PACKAGE_DIRECTORY]" ),
               this.NuGetMinClientVersion.ValueOrDefault( "[NUGET_CLIENT_MIN_VERSION]" ),
               this.PathToCommonBuildTools.ValueOrDefault( "[PATH_TO_COMMON_BUILD_TOOLS]" ),
               this.OutputAssemblyName.ValueOrDefault( pid ),
               originalPID
               );

            var output = this.BuildFileLocation;
            if ( String.IsNullOrEmpty( output ) )
            {
               this.Log.LogMessage( MessageImportance.High, buildFileContents );
            }
            else
            {
               using ( var stream = File.Open( output, FileMode.Create, FileAccess.Write, FileShare.Read ) )
               {
                  var bytez = Encoding.UTF8.GetBytes( buildFileContents );
                  stream.Write( bytez, 0, bytez.Length );
               }
            }

            retVal = true;
         }

         return retVal;
      }
   }
}

internal static partial class E_CBT
{
   internal static String ValueOrDefault( this String str, String defaultValue )
   {
      return String.IsNullOrEmpty( str ) ? defaultValue : str;
   }
}