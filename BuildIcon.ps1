$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
public static class IslandIconBuilder {
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
 public static void Build(string input,string output){
  int[] sizes={16,20,24,32,48,64,128,256};byte[][] entries=new byte[sizes.Length][],payloads=new byte[sizes.Length][];
  using(var source=Image.FromFile(input)){
   for(int i=0;i<sizes.Length;i++)using(var bitmap=new Bitmap(sizes[i],sizes[i],PixelFormat.Format32bppArgb)){
    using(var graphics=Graphics.FromImage(bitmap)){graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.CompositingQuality=CompositingQuality.HighQuality;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;graphics.DrawImage(source,0,0,sizes[i],sizes[i]);}
    using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory)){
     int size=sizes[i],maskStride=((size+31)/32)*4;
     writer.Write(40);writer.Write(size);writer.Write(size*2);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(0);writer.Write(size*size*4);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
     for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++){Color color=bitmap.GetPixel(x,y);writer.Write(color.B);writer.Write(color.G);writer.Write(color.R);writer.Write(color.A);}
     writer.Write(new byte[maskStride*size]);writer.Flush();payloads[i]=memory.ToArray();entries[i]=new byte[16];entries[i][0]=(byte)(size%256);entries[i][1]=(byte)(size%256);entries[i][4]=1;entries[i][6]=32;Array.Copy(BitConverter.GetBytes(payloads[i].Length),0,entries[i],8,4);
    }
   }
  }
  using(var writer=new BinaryWriter(File.Create(output))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){Array.Copy(BitConverter.GetBytes(offset),0,entries[i],12,4);writer.Write(entries[i]);offset+=payloads[i].Length;}foreach(var payload in payloads)writer.Write(payload);}
 }
}
'@ -ReferencedAssemblies System.Drawing
$bitmap=New-Object Drawing.Bitmap 256,256
$g=[Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$path=New-Object Drawing.Drawing2D.GraphicsPath
$path.AddArc(8,8,100,100,180,90);$path.AddArc(148,8,100,100,270,90);$path.AddArc(148,148,100,100,0,90);$path.AddArc(8,148,100,100,90,90);$path.CloseFigure()
$brush=New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.Point]::new(0,0)),([Drawing.Point]::new(256,256)),([Drawing.Color]::FromArgb(167,139,250)),([Drawing.Color]::FromArgb(237,114,173))
$g.FillPath($brush,$path)
$pen=New-Object Drawing.Pen ([Drawing.Color]::FromArgb(245,245,255)),12
$pen.StartCap='Round';$pen.EndCap='Round';$pen.LineJoin='Round'
$g.DrawLine($pen,94,63,112,80);$g.DrawLine($pen,162,63,144,80)
$g.DrawRectangle($pen,57,84,142,113)
$points=[Drawing.Point[]]@([Drawing.Point]::new(111,115),[Drawing.Point]::new(111,166),[Drawing.Point]::new(153,140))
$g.FillPolygon([Drawing.Brushes]::White,$points)
$bitmap.Save((Join-Path $PSScriptRoot 'mini.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bitmap.Dispose();$brush.Dispose();$pen.Dispose();$path.Dispose()
[IslandIconBuilder]::Build((Join-Path $PSScriptRoot 'mini.png'),(Join-Path $PSScriptRoot 'mini.ico'))