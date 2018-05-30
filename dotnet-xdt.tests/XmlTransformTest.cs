using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DotNet.Xdt.Tests
{
    public class XmlTransformTest
    {
        [Fact]
        public void Support_WriteToStream()
        {
            string source = TestResource("Web.config");
            string transformFile = TestResource("Web.Release.config");
            string destFile = OutputFile("MyWeb.config", nameof(Support_WriteToStream));

            //execute
            using (var x = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                x.Load(source);

                using (var transform = new XmlTransformation(transformFile))
                {
                    bool succeed = transform.Apply(x);

                    using (var fsDestFile = new FileStream(destFile, FileMode.OpenOrCreate))
                    {
                        x.Save(fsDestFile);

                        //verify, we have a success transform
                        Assert.True(succeed);

                        //verify, the stream is not closed
                        Assert.True(fsDestFile.CanWrite, "The file stream can not be written. was it closed?");
                    }

                    //sanity verify the content is right, (xml was transformed)
                    string content = File.ReadAllText(destFile);
                    Assert.DoesNotContain("debug=\"true\"", content);

                    //sanity verify the line format is not lost (otherwsie we will have only one long line)
                    Assert.InRange(File.ReadLines(destFile).Count(), 10, int.MaxValue);
                }
            }
        }

        [Fact]
        public void AttributeFormatting() => Transform_ExpectSuccess(nameof(AttributeFormatting));

        [Fact]
        public void TagFormatting() => Transform_ExpectSuccess(nameof(TagFormatting));

        //2 edge cases we didn't handle well and then fixed it per customer feedback.
        //    a. '>' in the attribute value
        //    b. element with only one character such as <p>
        [Fact]
        public void EdgeCase() => Transform_ExpectSuccess(nameof(EdgeCase));

        [Fact]
        public void WarningsAndErrors() => Transform_ExpectFail(nameof(WarningsAndErrors));

        static void Transform_ExpectSuccess(string baseFileName)
        {
            string src = TestResource($"{baseFileName}_source.xml");
            string transformFile = TestResource($"{baseFileName}_transform.xml");
            string baselineFile = TestResource($"{baseFileName}_baseline.xml");
            string destFile = OutputFile("result.xml", baseFileName);
            string expectedLog = TestResource($"{baseFileName}.log");
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
            Assert.True(succeed, baseFileName);
            Assert.Equal(File.ReadAllText(baselineFile), File.ReadAllText(destFile));
            Assert.Equal(File.ReadAllText(expectedLog), logger.LogText);
        }

        static void Transform_ExpectFail(string baseFileName)
        {
            string src = TestResource($"{baseFileName}_source.xml");
            string transformFile = TestResource($"{baseFileName}_transform.xml");
            string destFile = OutputFile("result.xml", baseFileName);
            string expectedLog = TestResource($"{baseFileName}.log");
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
            Assert.False(succeed, baseFileName);
            Assert.Equal(File.ReadAllText(expectedLog), logger.LogText);
        }

        static string TestResource(string fileName)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "Resources", fileName);
            if (File.Exists(path)) return path;
            throw new IOException($"Cannot not find test resource: {Path.GetFullPath(path)}");
        }

        static string OutputFile(string fileName, string testName)
        {
            if (!Directory.Exists(testName)) Directory.CreateDirectory(testName);
            return Path.Combine(testName, fileName);
        }
    }
}
