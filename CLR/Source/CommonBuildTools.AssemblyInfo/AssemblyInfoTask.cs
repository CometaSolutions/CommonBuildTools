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
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CSharp;
using Microsoft.VisualBasic;

namespace CommonBuildTools
{
   public class AssemblyInfoTask : Task
   {
      private const String LANG_CS = "cs";
      private const String LANG_VB = "vb";
      private const String LANG_CPP = "cpp";

      private const String DEFAULT_LANGUAGE = LANG_CS;

      private static readonly Type COPYRIGHT_ATTR = typeof( System.Reflection.AssemblyCopyrightAttribute );

      public String Language { get; set; }

      [Required]
      public String AssemblyAttributeXMLInfo { get; set; }

      [Required]
      public String OutputFile { get; set; }

      public Boolean AppendAssemblyCopyrightYears { get; set; }

      public String AssemblyInceptionYear { get; set; }

      public override Boolean Execute()
      {
         XElement assemblyInfo = null;
         var retVal = false;
         try
         {
            assemblyInfo = XElement.Load( new StringReader( this.AssemblyAttributeXMLInfo ) );
            retVal = true;
         }
         catch ( Exception exc )
         {
            this.Log.LogError( "Malformed assembly attribute XML: {0}.", exc.Message );
         }

         if ( retVal )
         {
            // All this assemblyCopyrightSuffix junk just because standard MSBuild doesn't provide to get current year via normal build task, and XBuild doesn't support property functions...
            // And I don't want to include whole community msbuild task assembly just for the current datetime getter...
            String assemblyCopyrightSuffix = null;
            Int32 inceptionYear;

            if ( this.AppendAssemblyCopyrightYears && Int32.TryParse( this.AssemblyInceptionYear, out inceptionYear ) )
            {
               assemblyCopyrightSuffix = " " + inceptionYear + "-" + DateTime.Now.Year;
            }

            try
            {
               retVal = this.Generate( assemblyInfo, assemblyCopyrightSuffix );
            }
            catch ( Exception exc )
            {
               retVal = false;
               this.Log.LogError( "Generation failed: {0}.", exc );
            }
         }

         return retVal;
      }

      private Boolean Generate( XElement assemblyInfo, String assemblyCopyrightSuffix )
      {
         var path = this.GetFilepath();

         var provider = this.GetProvider( ref path );

         var retVal = provider != null;

         if ( retVal )
         {

            var compileUnit = new CodeCompileUnit();
            var ns = new CodeNamespace();
            compileUnit.Namespaces.Add( ns );

            GenerateAttributes( assemblyInfo, compileUnit, assemblyCopyrightSuffix );

            String generatedString;
            using ( var writer = new StringWriter() )
            {
               var options = new CodeGeneratorOptions();
               provider.GenerateCodeFromCompileUnit( compileUnit, writer, options );
               generatedString = writer.ToString().Trim();
            }


            var dir = Path.GetDirectoryName( path );
            if ( !Directory.Exists( dir ) )
            {
               try
               {
                  Directory.CreateDirectory( dir );
               }
               catch
               {
                  // Ignore, might happen in e.g. parallel cases
               }
            }

            // Check that we won't refresh the information for nothing. This will cause long builds
            if ( !File.Exists( path ) || !File.ReadAllText( path ).Trim().Equals( generatedString, StringComparison.CurrentCultureIgnoreCase ) )
            {
               this.Log.LogMessage( MessageImportance.High, "Generating assembly info path to {0}.", path );
               File.WriteAllText( path, generatedString );
            }
            else
            {
               this.Log.LogMessage( MessageImportance.High, "Skipping assembly info path generation to {0} as it is up to date.", path );
            }
         }

         return retVal;
      }

      private String GetFilepath()
      {
         return Path.GetFullPath( this.OutputFile );
      }

      private static void GenerateAttributes( XElement assemblyInfo, CodeCompileUnit compileUnit, String assemblyCopyrightSuffix )
      {
         foreach ( var attributeElement in assemblyInfo.XPathSelectElements( "AssemblyAttribute" ) )
         {
            var ns = attributeElement.Attribute( "Namespace" ).Value;
            var tn = attributeElement.Attribute( "Name" ).Value;
            var attr = new CodeAttributeDeclaration( ns + "." + tn );
            String suffix = null;

            var cmp = tn;
            if ( !cmp.EndsWith( "Attribute" ) )
            {
               cmp += "Attribute";
            }
            if ( String.Equals( COPYRIGHT_ATTR.Namespace, ns ) && String.Equals( COPYRIGHT_ATTR.Name, cmp ) )
            {
               suffix = assemblyCopyrightSuffix;
            }

            foreach ( var ctorArgElement in attributeElement.XPathSelectElements( "ConstructorArgument" ) )
            {
               attr.Arguments.Add( new CodeAttributeArgument( CreateCodeExpression( ctorArgElement, suffix ) ) );
            }

            foreach ( var namedArgElement in attributeElement.XPathSelectElements( "NamedArgument" ) )
            {
               attr.Arguments.Add( new CodeAttributeArgument( namedArgElement.Attribute( "Name" ).Value, CreateCodeExpression( namedArgElement, null ) ) );
            }

            compileUnit.AssemblyCustomAttributes.Add( attr );
         }
      }

      private static CodeExpression CreateCodeExpression( XElement expressionElement, String suffix )
      {
         var value = CreateCodeExpressionValue( expressionElement.Elements().First() );
         if ( suffix != null )
         {
            value = "" + value + suffix;
         }
         return new CodePrimitiveExpression( value );
      }

      private static Object CreateCodeExpressionValue( XElement expressionElement )
      {
         switch ( expressionElement.Name.LocalName )
         {
            case "Literal":
               var tc = (TypeCode) Enum.Parse( typeof( TypeCode ), expressionElement.Attribute( "TypeCode" ).Value );
               var str = expressionElement.Value;
               try
               {
                  switch ( tc )
                  {
                     case TypeCode.Empty:
                     case TypeCode.Object:
                     case TypeCode.DBNull:
                        return null;
                     case TypeCode.Boolean:
                        Boolean bol;
                        return Boolean.TryParse( str, out bol ) && bol;
                     case TypeCode.Char:
                        return str[0];
                     case TypeCode.SByte:
                        return SByte.Parse( str );
                     case TypeCode.Byte:
                        return Byte.Parse( str );
                     case TypeCode.Int16:
                        return Int16.Parse( str );
                     case TypeCode.UInt16:
                        return UInt16.Parse( str );
                     case TypeCode.Int32:
                        return Int32.Parse( str );
                     case TypeCode.UInt32:
                        return UInt32.Parse( str );
                     case TypeCode.Int64:
                        return Int64.Parse( str );
                     case TypeCode.UInt64:
                        return UInt64.Parse( str );
                     case TypeCode.Single:
                        return Single.Parse( str );
                     case TypeCode.Double:
                        return Double.Parse( str );
                     case TypeCode.Decimal:
                        return Decimal.Parse( str );
                     case TypeCode.DateTime:
                        return DateTime.Parse( str );
                     case TypeCode.String:
                        return str;
                     default:
                        throw new InvalidOperationException( "Unrecognized typecode: " + tc );
                  }
               }
               catch ( Exception exc )
               {
                  throw new InvalidDataException( "Failed to parse " + expressionElement + ".", exc );
               }
            case "ReadPublicKeyFrom":
               throw new NotImplementedException( "TODO" );
            case "Concat":
               return String.Join( "", expressionElement.Elements().Select( el => CreateCodeExpressionValue( el ) ) );
            default:
               throw new InvalidOperationException( "Unrecognized argument data element: " + expressionElement.Name + "." );
         }
      }

      private CodeDomProvider GetProvider( ref String path )
      {
         var lang = this.Language;
         if ( String.IsNullOrEmpty( lang ) )
         {
            lang = DEFAULT_LANGUAGE;
         }

         lang = lang.ToLower();

         CodeDomProvider retVal;
         switch ( lang )
         {
            case LANG_CS:
               retVal = new CSharpCodeProvider();
               path = Path.ChangeExtension( path, ".cs" );
               break;
            case LANG_VB:
               retVal = new VBCodeProvider();
               path = Path.ChangeExtension( path, ".vb" );
               break;
            case LANG_CPP:
               retVal = null;
               try
               {
                  var ass = System.Reflection.Assembly.Load( "CppCodeProvider, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" );
                  retVal = (CodeDomProvider) ass.CreateInstance( "Microsoft.VisualC.CppCodeProvider" );
               }
               catch ( FileNotFoundException )
               {
                  this.Log.LogError( "Could not find CPP code provider assembly (CppCodeProvider), make sure you have VisualC++ installed." );
               }
               catch ( Exception exc )
               {
                  String fusion = null;
                  var msg = "Error when creating CPP code provider: {0}";
                  if ( exc is FileLoadException )
                  {
                     fusion = ( (FileLoadException) exc ).FusionLog;
                     this.Log.LogError( msg + ", fusion log file: {1}.", exc.Message, ( fusion ?? "<fusion logging not enabled>" ) );
                  }
                  else
                  {
                     this.Log.LogError( msg + ".", exc.Message );
                  }
               }
               path = Path.ChangeExtension( path, ".cpp" );
               break;
            default:
               this.Log.LogError( "Unrecognized language: " + lang + "." );
               retVal = null;
               break;
         }

         return retVal;
      }
   }
}
