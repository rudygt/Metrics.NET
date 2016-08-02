using FluentAssertions;
using Metrics.Visualization;
using Xunit;
using Xunit.Abstractions;

namespace Metrics.Tests.Visualization
{
    public class FlotVisualizationTests
    {      
        [Fact]
        public void FlotVisualization_CanReadAppFromResource()
        {            
            var html = FlotWebApp.GetFlotApp();
            html.Should().NotBeEmpty();

            /*Action createHtml = () => CQ.CreateDocument(html);
            createHtml.ShouldNotThrow();*/
        }
    }
}
