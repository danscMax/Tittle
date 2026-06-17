using Tittle.Core.Text;
using Tittle.Features.Shell;
using Xunit;

namespace Tittle.Tests.Features;

public class DocumentTabSaveEncodingTests
{
    [Fact]
    public void SaveEncodingName_DefaultsToUtf8_MatchingThePriorPolicy()
    {
        var vm = DocumentTabViewModel.FromFile("text", "/x.txt");
        Assert.Equal(SaveEncoding.Utf8, vm.SaveEncodingName);
    }

    [Fact]
    public void SetSaveEncoding_ChangesTheTarget()
    {
        var vm = DocumentTabViewModel.FromFile("text", "/x.txt");

        vm.SetSaveEncodingCommand.Execute(SaveEncoding.Windows1251);

        Assert.Equal(SaveEncoding.Windows1251, vm.SaveEncodingName);
    }
}
