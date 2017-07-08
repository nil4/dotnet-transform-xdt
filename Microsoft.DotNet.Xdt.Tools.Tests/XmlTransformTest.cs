using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.DotNet.Xdt.Tools.Tests
{
    [TestClass]
    public class XmlTransformTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void XmlTransform_Support_WriteToStream()
        {
            string src = CreateATestFile("Web.config", Properties.Resources.Web);
            string transformFile = CreateATestFile("Web.Release.config", Properties.Resources.Web_Release);
            string destFile = GetTestFilePath("MyWeb.config");

            //execute
            using (var x = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                x.Load(src);

                using (var transform = new XmlTransformation(transformFile))
                {
                    bool succeed = transform.Apply(x);

                    using (var fsDestFile = new FileStream(destFile, FileMode.OpenOrCreate))
                    {
                        x.Save(fsDestFile);

                        //verify, we have a success transform
                        Assert.IsTrue(succeed);

                        //verify, the stream is not closed
                        Assert.IsTrue(fsDestFile.CanWrite, "The file stream can not be written. was it closed?");
                    }

                    //sanity verify the content is right, (xml was transformed)
                    string content = File.ReadAllText(destFile);
                    Assert.IsFalse(content.Contains("debug=\"true\""));

                    //sanity verify the line format is not lost (otherwsie we will have only one long line)
                    Assert.IsTrue(File.ReadLines(destFile).Take(11).Count() > 10);
                }
            }
        }

        [TestMethod]
        public void XmlTransform_AttibuteFormatting()
        {
            Transform_TestRunner_ExpectSuccess(Properties.Resources.AttributeFormating_source,
                    Properties.Resources.AttributeFormating_transform,
                    Properties.Resources.AttributeFormating_destination,
                    Properties.Resources.AttributeFormatting_log);
        }

        [TestMethod]
        public void XmlTransform_TagFormatting()
        {
             Transform_TestRunner_ExpectSuccess(Properties.Resources.TagFormatting_source,
                    Properties.Resources.TagFormatting_transform,
                    Properties.Resources.TagFormatting_destination,
                    Properties.Resources.TagFormatting_log);
        }

        [TestMethod]
        public void XmlTransform_HandleEdgeCase()
        {
            //2 edge cases we didn't handle well and then fixed it per customer feedback.
            //    a. '>' in the attribute value
            //    b. element with only one character such as <p>
            Transform_TestRunner_ExpectSuccess(Properties.Resources.EdgeCase_source,
                    Properties.Resources.EdgeCase_transform,
                    Properties.Resources.EdgeCase_destination,
                    Properties.Resources.EdgeCase_log);
        }

        [TestMethod]
        public void XmlTransform_ErrorAndWarning()
        {
            Transform_TestRunner_ExpectFail(Properties.Resources.WarningsAndErrors_source,
                    Properties.Resources.WarningsAndErrors_transform,
                    Properties.Resources.WarningsAndErrors_log);
        }

        private void Transform_TestRunner_ExpectSuccess(string source, string transform, string baseline, string expectedLog)
        {
            string src = CreateATestFile("source.config", source);
            string transformFile = CreateATestFile("transform.config", transform);
            string baselineFile = CreateATestFile("baseline.config", baseline);
            string destFile = GetTestFilePath("result.config");
            var logger = new TestTransformationLogger();

            bool succeed;
            using (var x = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                x.Load(src);

                using (var xmlTransform = new XmlTransformation(transformFile, logger))
                {
                    //execute
                    succeed = xmlTransform.Apply(x);
                    x.Save(destFile);
                }
            }
            
            //test
            Assert.IsTrue(succeed);
            CompareFiles(destFile, baselineFile);
            CompareMultiLines(expectedLog, logger.LogText);
        }

        private void Transform_TestRunner_ExpectFail(string source, string transform, string expectedLog)
        {
            string src = CreateATestFile("source.config", source);
            string transformFile = CreateATestFile("transform.config", transform);
            string destFile = GetTestFilePath("result.config");
            var logger = new TestTransformationLogger();

            bool succeed;
            using (var x = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                x.Load(src);

                using (var xmlTransform = new XmlTransformation(transformFile, logger))
                {
                    //execute
                    succeed = xmlTransform.Apply(x);
                    x.Save(destFile);
                    xmlTransform.Dispose();
                }
                x.Dispose();
            }
            
            //test
            Assert.IsFalse(succeed);
            CompareMultiLines(expectedLog, logger.LogText);
        }

        private static void CompareFiles(string baseLinePath, string resultPath)
        {
            string bsl = File.ReadAllText(baseLinePath);
            string result = File.ReadAllText(resultPath);

            CompareMultiLines(bsl, result);
        }

        private static void CompareMultiLines(string baseline, string result)
        {
            var baseLines = baseline.Split(new[] { Environment.NewLine },  StringSplitOptions.None);
            var resultLines = result.Split(new[] { Environment.NewLine },  StringSplitOptions.None);

            for (var i = 0; i < baseLines.Length; i++)
            {
                Assert.AreEqual(baseLines[i], resultLines[i], string.Format("line {0} at baseline file is not matched", i));
            }
        }

        private string CreateATestFile(string filename, string contentsFilePath)
        {
            string file = GetTestFilePath(filename);
            string sourceFilePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), contentsFilePath));
            Console.WriteLine($"Copy {sourceFilePath} -> {file}");
            File.WriteAllText(file, File.ReadAllText(sourceFilePath));
            return file;
        }

        private string GetTestFilePath(string filename)
        {
            //string folder = Path.Combine(TestContext.TestDeploymentDir, TestContext.TestName);
            string folder = Path.Combine(Environment.CurrentDirectory, TestContext.TestName);
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, filename);
            Console.WriteLine($"TestFile {file}");
            return file;
        }
    }
}
