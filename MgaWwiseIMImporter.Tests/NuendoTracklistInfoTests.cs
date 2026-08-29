using System.IO;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Nuendo;

namespace MgaWwiseIMImporter.Tests;

public class NuendoTracklistInfoTests
{
    [Fact]
    public void Read_LinearMarkerTrack_ThrowsMusicalTimeBaseError()
    {
        var path = WriteTempXml(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <tracklist2>
              <list name="track" type="obj">
                <obj class="MMarkerTrackEvent" ID="1">
                  <obj class="MListNode" name="Node" ID="2">
                    <string name="Name" value="Marker" wide="true"/>
                    <member name="Domain">
                      <int name="Type" value="1"/>
                      <float name="Period" value="1"/>
                    </member>
                    <list name="Events" type="obj"/>
                  </obj>
                </obj>
              </list>
              <obj class="PArrangeSetup" name="Setup" ID="3">
                <member name="Length">
                  <member name="Domain">
                    <int name="Type" value="1"/>
                    <float name="Period" value="1"/>
                  </member>
                </member>
              </obj>
            </tracklist2>
            """);

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => NuendoTracklistInfo.Read(path));
            Assert.Equal(UiStrings.ErrMarkerTrackLinearTimeBase, ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_NoTempoAndNoMarker_ThrowsTempoTrackMissing()
    {
        var path = WriteTempXml(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <tracklist2>
              <list name="track" type="obj"/>
            </tracklist2>
            """);

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => NuendoTracklistInfo.Read(path));
            Assert.Equal(UiStrings.ErrTempoTrackMissing, ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MusicalMarkerTrack_LoadsTempo()
    {
        var path = WriteTempXml(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <tracklist2>
              <list name="track" type="obj">
                <obj class="MMarkerTrackEvent" ID="1">
                  <obj class="MListNode" name="Node" ID="2">
                    <member name="Domain">
                      <int name="Type" value="0"/>
                      <obj class="MTempoTrackEvent" name="Tempo Track" ID="3">
                        <list name="TempoEvent" type="obj">
                          <obj class="MTempoEvent" ID="4">
                            <float name="BPM" value="92"/>
                            <float name="PPQ" value="0"/>
                          </obj>
                        </list>
                      </obj>
                      <obj class="MSignatureTrackEvent" name="Signature Track" ID="5">
                        <list name="SignatureEvent" type="obj">
                          <obj class="MTimeSignatureEvent" ID="6">
                            <int name="Numerator" value="4"/>
                            <int name="Denominator" value="4"/>
                            <int name="Bar" value="0"/>
                            <int name="Position" value="0"/>
                          </obj>
                        </list>
                      </obj>
                    </member>
                    <list name="Events" type="obj"/>
                  </obj>
                </obj>
              </list>
            </tracklist2>
            """);

        try
        {
            var tracklist = NuendoTracklistInfo.Read(path);
            Assert.Equal(92d, Assert.Single(tracklist.TempoEvents).Bpm);
            Assert.Equal(4, Assert.Single(tracklist.SignatureEvents).Numerator);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempXml(string xml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mga-tracklist-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml);
        return path;
    }
}
