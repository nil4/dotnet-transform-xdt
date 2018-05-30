using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Shouldly;

namespace DotNet.Xdt.Tests
{
    public class XmlTransformTests
    {
        public void Support_WriteToStream()
        {
            string source = TestResource($"Web.config");
            string transformFile = TestResource($"Web.Release.config");
            string destFile = OutputFile("MyWeb.config", nameof(Support_WriteToStream));

            //execute
            using (var x = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                x.Load(source);

                using (var transform = new XmlTransformation(transformFile))
                {
                    bool applied = transform.Apply(x);

                    using (var fsDestFile = new FileStream(destFile, FileMode.OpenOrCreate))
                    {
                        x.Save(fsDestFile);

                        //verify, we have a success transform
                        applied.ShouldBeTrue();

                        //verify, the stream is not closed
                        fsDestFile.CanWrite.ShouldBeTrue("The file stream can not be written. was it closed?");
                    }

                    //sanity verify the content is right, (xml was transformed)
                    string content = File.ReadAllText(destFile);
                    content.ShouldNotContain("debug=\"true\"");

                    //sanity verify the line format is not lost (otherwsie we will have only one long line)
                    File.ReadLines(destFile).Count().ShouldBeGreaterThan(10);
                }
            }
        }

        public void AttributeFormatting() 
            => Transform_ExpectSuccess(nameof(AttributeFormatting));

        public void TagFormatting() 
            => Transform_ExpectSuccess(nameof(TagFormatting));

        //2 edge cases we didn't handle well and then fixed it per customer feedback.
        //    a. '>' in the attribute value
        //    b. element with only one character such as <p>
        public void EdgeCase() 
            => Transform_ExpectSuccess(nameof(EdgeCase));

        public void WarningsAndErrors() 
            => Transform_ExpectFail(nameof(WarningsAndErrors));

        static void Transform_ExpectSuccess(string baseFileName)
        {
            string src = TestResource($"{baseFileName}_source.xml");
            string transformFile = TestResource($"{baseFileName}_transform.xml");
            string baselineFile = TestResource($"{baseFileName}_baseline.xml");
            string destFile = OutputFile("result.xml", baseFileName);
            string expectedLog = TestResource($"{baseFileName}.log");
            var logger = new TestTransformationLogger();

            bool applied;
            using (var x = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                x.Load(src);

                using (var xmlTransform = new XmlTransformation(transformFile, logger))
                {
                    //execute
                    applied = xmlTransform.Apply(x);
                    x.Save(destFile);
                }
            }
            
            //test
            applied.ShouldBeTrue(baseFileName);
            File.ReadAllText(destFile).ShouldBe(File.ReadAllText(baselineFile));
            logger.LogText.ShouldBe(File.ReadAllText(expectedLog));
        }

        static void Transform_ExpectFail(string baseFileName)
        {
            string src = TestResource($"{baseFileName}_source.xml");
            string transformFile = TestResource($"{baseFileName}_transform.xml");
            string destFile = OutputFile("result.xml", baseFileName);
            string expectedLog = TestResource($"{baseFileName}.log");
            var logger = new TestTransformationLogger();

            bool applied;
            using (var x = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                x.Load(src);

                using (var xmlTransform = new XmlTransformation(transformFile, logger))
                {
                    //execute
                    applied = xmlTransform.Apply(x);
                    x.Save(destFile);
                }
            }
            
            //test
            applied.ShouldBeFalse(baseFileName);
            logger.LogText.ShouldBe(File.ReadAllText(expectedLog));
        }

        static string TestResource(FormattableString fileName)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "Resources", fileName.ToString(CultureInfo.InvariantCulture));
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
