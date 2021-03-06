﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenDiablo2.Common.Exceptions;

namespace OpenDiablo2.Common.Models
{
    public sealed class MPQDT1Block
    {
        public Int16 PositionX { get; internal set; }
        public Int16 PositionY { get; internal set; }
        public byte GridX { get; internal set; }
        public byte GridY { get; internal set; }
        public Int16 Format { get; internal set; }
        public Int32 Length { get; internal set; }
        public Int32 FileOffset { get; internal set; }
        public Int16[] PixelData { get; internal set; }
    }

    public sealed class MPQDT1Tile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Int32 Direction { get; internal set; }
        public Int16 RoofHeight { get; internal set; }
        public byte SoundIndex { get; internal set; }
        public bool Animated { get; internal set; }
        public int Height { get; internal set; }
        public int Width { get; internal set; }
        public Int32 Orientation { get; internal set; }
        public Int32 MainIndex { get; internal set; }
        public Int32 SubIndex { get; internal set; }
        public Int32 RarityOrFrameIndex { get; internal set; }// Frame index if animated floor tile
        public byte[] SubTileFlags { get; internal set; } = new byte[25];
        public Int32 BlockHeadersPointer { get; internal set; } // Pointer to block headers for this tile
        public Int32 BlockDataLength { get; internal set; } // Block headers + block data of this tile
        public Int32 NumberOfBlocks { get; internal set; }
        public MPQDT1Block[] Blocks { get; internal set; }
    }

    public sealed class MPQDT1
    {
        public Int32 X1 { get; private set; }
        public Int32 X2 { get; private set; }
        public Int32 NumberOfTiles { get; private set; }
        public MPQDT1Tile[] Tiles { get; private set; }
        private readonly Int32 _tileHeaderOffset;

        public MPQDT1(Stream stream)
        {
            var br = new BinaryReader(stream);

            X1 = br.ReadInt32();
            X2 = br.ReadInt32();

            stream.Seek(268, SeekOrigin.Begin); // Skip useless header info

            NumberOfTiles = br.ReadInt32();
            _tileHeaderOffset = br.ReadInt32();

            ReadTiles(br);
            ReadBlockHeaders(br);
            ReadBlockGraphics(br);

        }
        private void ReadTiles(BinaryReader br)
        {
            Tiles = new MPQDT1Tile[NumberOfTiles];
            for (var tileIndex = 0; tileIndex < NumberOfTiles; tileIndex++)
            {
                br.BaseStream.Seek(_tileHeaderOffset + (tileIndex * 96), SeekOrigin.Begin);
                Tiles[tileIndex] = new MPQDT1Tile();
                var tile = Tiles[tileIndex];

                tile.Direction = br.ReadInt32();
                tile.RoofHeight = br.ReadInt16();
                tile.SoundIndex = br.ReadByte();
                tile.Animated = br.ReadByte() == 1;
                tile.Height = br.ReadInt32();
                tile.Width = br.ReadInt32();
                br.ReadBytes(4);
                tile.Orientation = br.ReadInt32();
                tile.MainIndex = br.ReadInt32();
                tile.SubIndex = br.ReadInt32();
                tile.RarityOrFrameIndex = br.ReadInt32();
                br.ReadBytes(4);
                for (var i = 0; i < 25; i++)
                    tile.SubTileFlags[i] = br.ReadByte();
                br.ReadBytes(7);
                tile.BlockHeadersPointer = br.ReadInt32();
                tile.BlockDataLength = br.ReadInt32();
                tile.NumberOfBlocks = br.ReadInt32();
                br.ReadBytes(12);
            }
        }


        private void ReadBlockHeaders(BinaryReader br)
        {
            for (var tileIndex = 0; tileIndex < NumberOfTiles; tileIndex++)
            {
                var tile = Tiles[tileIndex];
                tile.Blocks = new MPQDT1Block[tile.NumberOfBlocks];

                for (var blockIndex = 0; blockIndex < tile.NumberOfBlocks; blockIndex++)
                {
                    br.BaseStream.Seek(tile.BlockHeadersPointer + (blockIndex * 20), SeekOrigin.Begin);

                    tile.Blocks[blockIndex] = new MPQDT1Block();
                    var block = tile.Blocks[blockIndex];

                    block.PositionX = br.ReadInt16();
                    block.PositionY = br.ReadInt16();
                    br.ReadBytes(2);
                    block.GridX = br.ReadByte();
                    block.GridX = br.ReadByte();
                    block.Format = br.ReadInt16();
                    block.Length = br.ReadInt32();
                    br.ReadBytes(2);
                    block.FileOffset = br.ReadInt32();
                }
            }
        }

        private void ReadBlockGraphics(BinaryReader br)
        {
            for (var tileIndex = 0; tileIndex < NumberOfTiles; tileIndex++)
            {
                for (var blockIndex = 0; blockIndex < Tiles[tileIndex].NumberOfBlocks; blockIndex++)
                {
                    var block = Tiles[tileIndex].Blocks[blockIndex];
                    br.BaseStream.Seek(Tiles[tileIndex].BlockHeadersPointer + block.FileOffset, SeekOrigin.Begin);

                    if (block.Format == 1)
                    {
                        // 3D isometric block
                        if (block.Length != 256)
                            throw new OpenDiablo2Exception($"Expected exactly 256 bytes of data, but got {block.Length} instead!");

                        var y = 0;
                        var length = 256;
                        block.PixelData = new Int16[32 * 32];
                        while (length > 0)
                        {
                            var x = new[] { 14, 12, 10, 8, 6, 4, 2, 0, 2, 4, 6, 8, 10, 12, 14 }[y];
                            var n = new[] { 4, 8, 12, 16, 20, 24, 28, 32, 28, 24, 20, 16, 12, 8, 4 }[y];
                            length -= n;
                            while (n > 0)
                            {
                                block.PixelData[x + (y * 32)] = br.ReadByte();
                                x++;
                                n--;
                            }
                            y++;
                        }

                    }
                    else
                    {
                        // RLE block
                        var length = block.Length;
                        var x = 0;
                        var y = 0;
                        block.PixelData = new Int16[32 * 32];
                        while (length > 0)
                        {
                            var b1 = br.ReadByte();
                            var b2 = br.ReadByte();
                            length -= 2;
                            if (b1 + b2 == 0)
                            {
                                x = 0;
                                y++;
                                continue;
                            }

                            x += b1;
                            length -= b2;
                            while (b2 > 0)
                            {
                                block.PixelData[x + (y * 32)] = br.ReadByte();
                                x++;
                                b2--;
                            }
                        }
                    }
                }
            }
        }

    }
}
