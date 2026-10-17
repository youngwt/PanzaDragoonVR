using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PdzDecoding.Tests;

/// <summary>Reading a cue sheet and the ISO9660 file system of bin/cue images and plain ISOs built here.</summary>
public sealed class DiscImageTests : IDisposable
{
    private readonly string _folder;

    public DiscImageTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pdz-disc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void CueSheet_FindsTrack1FileAndStart()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Game (Track 1).bin"), new byte[16]);
        string cue = Path.Combine(_folder, "Game.cue");

        DataTrack track = CueSheet.Parse(SyntheticDisc.Cue("Game (Track 1).bin"), cue);

        Assert.Equal(Path.Combine(_folder, "Game (Track 1).bin"), track.BinPath);
        Assert.Equal(0, track.StartSector);
        Assert.Equal(DiscImage.RawSectorSize, track.SectorSize);
    }

    [Fact]
    public void CueSheet_FindsTrack1InAFileItSharesWithTheAudio()
    {
        string cue = new SyntheticDisc().WriteSingleFileCue(_folder);

        DataTrack track = CueSheet.Read(cue);

        Assert.Equal(Path.Combine(_folder, "Game.bin"), track.BinPath);
        Assert.Equal(0, track.StartSector);
        Assert.Equal(DiscImage.RawSectorSize, track.SectorSize);
    }

    [Fact]
    public void CueSheet_ReadsAMode1_2048TrackAs2048ByteSectors()
    {
        File.WriteAllBytes(Path.Combine(_folder, "a.bin"), new byte[16]);

        DataTrack track = CueSheet.Parse(SyntheticDisc.Cue("a.bin", "MODE1/2048"), Path.Combine(_folder, "a.cue"));

        Assert.Equal(DiscImage.SectorSize, track.SectorSize);
    }

    [Fact]
    public void CueSheet_RejectsAnAudioTrack1()
    {
        File.WriteAllBytes(Path.Combine(_folder, "a.bin"), new byte[16]);

        var e = Assert.Throws<DecodingException>(() => CueSheet.Parse(SyntheticDisc.Cue("a.bin", "AUDIO"), Path.Combine(_folder, "a.cue")));

        Assert.Equal($"{Path.Combine(_folder, "a.cue")}: track 1 is AUDIO, and only MODE1/2352 or MODE1/2048 is read", e.Message);
    }

    [Fact]
    public void CueSheet_ReadsAnIndexOffsetAsSectors()
    {
        File.WriteAllBytes(Path.Combine(_folder, "a.bin"), new byte[16]);
        string text = "FILE \"a.bin\" BINARY\n  TRACK 01 MODE1/2352\n    INDEX 00 00:00:00\n    INDEX 01 00:02:05\n";

        Assert.Equal(2 * 75 + 5, CueSheet.Parse(text, Path.Combine(_folder, "a.cue")).StartSector);
    }

    [Fact]
    public void CueSheet_NamesAMissingBin()
    {
        var e = Assert.Throws<DecodingException>(() => CueSheet.Parse(SyntheticDisc.Cue("gone.bin"), Path.Combine(_folder, "Game.cue")));

        Assert.Contains("names the file 'gone.bin', which is missing", e.Message);
    }

    [Fact]
    public void CueSheet_RejectsAnotherModeForTrack1()
    {
        File.WriteAllBytes(Path.Combine(_folder, "a.bin"), new byte[16]);

        var e = Assert.Throws<DecodingException>(() => CueSheet.Parse(SyntheticDisc.Cue("a.bin", "MODE2/2352"), Path.Combine(_folder, "a.cue")));

        Assert.Contains("track 1 is MODE2/2352", e.Message);
    }

    [Fact]
    public void DiscImage_ListsFilesInFoldersAndPastSectorPadding()
    {
        string cue = new SyntheticDisc { PadRootDirectory = true }
            .With("A.MDB", new byte[] { 1, 2, 3 })
            .With("B.CGB", new byte[5000])
            .With("C.DAT", new byte[] { 9 }, folder: "SUB")
            .WriteCue(_folder);

        using DiscImage disc = DiscImage.OpenCue(cue);

        Assert.Equal(new[] { "A.MDB", "B.CGB", "SUB/C.DAT" }, disc.Files.Select(f => f.Path));
        Assert.Equal(5000u, disc.Find("b.cgb")!.Value.Size);
    }

    [Fact]
    public void DiscImage_ReadsAFileAcrossSectorsWithoutTheirHeaders()
    {
        byte[] data = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
        string cue = new SyntheticDisc().With("B.CGB", data).WriteCue(_folder);

        using DiscImage disc = DiscImage.OpenCue(cue);

        Assert.Equal(data, disc.ReadFile(disc.Find("B.CGB")!.Value));
    }

    [Fact]
    public void DiscImage_ReadsTheSaturnHeader()
    {
        string cue = new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.WriteCue(_folder);

        using DiscImage disc = DiscImage.OpenCue(cue);
        SaturnHeader header = disc.Header();

        Assert.Equal("SEGA SEGASATURN", header.Hardware);
        Assert.Equal("GS-9049", header.Product);
        Assert.Equal("V1.001", header.Version);
    }

    [Fact]
    public void DiscImage_ReadsASingleFileCueAsItsPerTrackLayout()
    {
        byte[] data = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
        var disc = new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.With("B.CGB", data, folder: "SUB");

        using DiscImage single = DiscImage.OpenImage(disc.WriteSingleFileCue(_folder));

        Assert.Equal(new[] { "SUB/B.CGB" }, single.Files.Select(f => f.Path));
        Assert.Equal(data, single.ReadFile(single.Find("SUB/B.CGB")!.Value));
        Assert.Equal("GS-9049", single.Header().Product);
    }

    [Fact]
    public void DiscImage_ReadsAPlainIsoFromSector16AndItsHeaderFromSector0()
    {
        byte[] data = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
        string iso = new SyntheticDisc { Product = "MK-81022", Version = "V1.000", PadRootDirectory = true }
            .With("A.MDB", new byte[] { 1, 2, 3 })
            .With("B.CGB", data, folder: "SUB")
            .WriteIso(_folder);

        using DiscImage disc = DiscImage.OpenImage(iso);

        Assert.Equal(new[] { "A.MDB", "SUB/B.CGB" }, disc.Files.Select(f => f.Path));
        Assert.Equal(data, disc.ReadFile(disc.Find("SUB/B.CGB")!.Value));
        Assert.Equal("MK-81022", disc.Header().Product);
        Assert.Equal("V1.000", disc.Header().Version);
    }

    [Fact]
    public void DiscImage_FailsOnAnIsoWithNoVolumeAtSector16()
    {
        string iso = Path.Combine(_folder, "Game.iso");
        File.WriteAllBytes(iso, new byte[20 * DiscImage.SectorSize]);

        var e = Assert.Throws<DecodingException>(() => DiscImage.OpenImage(iso));

        Assert.Equal("Game.iso: no ISO9660 volume at sector 16", e.Message);
    }

    [Fact]
    public void DiscImage_FailsOnAnIsoTooShortForAVolume()
    {
        string iso = Path.Combine(_folder, "Game.iso");
        File.WriteAllBytes(iso, new byte[16 * DiscImage.SectorSize]);

        var e = Assert.Throws<DecodingException>(() => DiscImage.OpenImage(iso));

        Assert.Equal("Game.iso: no ISO9660 volume at sector 16: the image is too short", e.Message);
    }

    [Fact]
    public void DiscImage_RejectsAFileThatIsNotADiscImage()
    {
        string bin = Path.Combine(_folder, "Game.bin");
        File.WriteAllBytes(bin, new byte[16]);

        var e = Assert.Throws<DecodingException>(() => DiscImage.OpenImage(bin));

        Assert.Equal($"{bin}: not a disc image: only a .cue sheet or an .iso is read", e.Message);
    }

    [Fact]
    public void DiscImage_FailsWithNoIsoVolumeAtSector16()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Game (Track 1).bin"), new byte[20 * DiscImage.RawSectorSize]);
        string cue = Path.Combine(_folder, "Game.cue");
        File.WriteAllText(cue, SyntheticDisc.Cue("Game (Track 1).bin"));

        var e = Assert.Throws<DecodingException>(() => DiscImage.OpenCue(cue));

        Assert.Contains("no ISO9660 volume at sector 16", e.Message);
    }

    [Fact]
    public void DiscImage_FailsOnAnImageTooShortForAVolume()
    {
        File.WriteAllBytes(Path.Combine(_folder, "Game (Track 1).bin"), new byte[100]);
        string cue = Path.Combine(_folder, "Game.cue");
        File.WriteAllText(cue, SyntheticDisc.Cue("Game (Track 1).bin"));

        var e = Assert.Throws<DecodingException>(() => DiscImage.OpenCue(cue));

        Assert.Contains("no ISO9660 volume at sector 16", e.Message);
    }
}
