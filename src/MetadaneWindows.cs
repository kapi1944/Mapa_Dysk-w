using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MapaDyskow {
public static class MetadaneWindows {
    [StructLayout(LayoutKind.Sequential)] struct RozmiarObrazu {public int Szerokosc,Wysokosc;}
    [StructLayout(LayoutKind.Sequential,Pack=4)] struct KluczWlasciwosci {public Guid Format;public uint Id;}
    [StructLayout(LayoutKind.Explicit,Size=24)] struct WartoscWlasciwosci {[FieldOffset(0)]public ushort Typ;[FieldOffset(8)]public ulong Liczba;[FieldOffset(8)]public uint MalaLiczba;[FieldOffset(8)]public int Calkowita;[FieldOffset(8)]public long Dluga;}
    [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface FabrykaObrazow {
        [PreserveSig]int PobierzObraz(RozmiarObrazu rozmiar,int flagi,out IntPtr bitmapa);
    }
    // Kolejność trzech metod jest częścią ABI IPropertyStore. Nie deklarujemy metod zapisu.
    [ComImport,Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface MagazynWlasciwosci {
        [PreserveSig]int PobierzLiczbe(out uint liczba);
        [PreserveSig]int PobierzKlucz(uint indeks,out KluczWlasciwosci klucz);
        [PreserveSig]int PobierzWartosc(ref KluczWlasciwosci klucz,out WartoscWlasciwosci wartosc);
    }
    [DllImport("shell32.dll",EntryPoint="SHCreateItemFromParsingName",CharSet=CharSet.Unicode)]static extern int UtworzElement(string sciezka,IntPtr kontekst,ref Guid id,out FabrykaObrazow fabryka);
    [DllImport("shell32.dll",EntryPoint="SHGetPropertyStoreFromParsingName",CharSet=CharSet.Unicode)]static extern int OtworzWlasciwosci(string sciezka,IntPtr kontekst,int flagi,ref Guid id,out MagazynWlasciwosci magazyn);
    [DllImport("propsys.dll",EntryPoint="PSGetPropertyKeyFromName",CharSet=CharSet.Unicode)]static extern int KluczZNazwy(string nazwa,out KluczWlasciwosci klucz);
    [DllImport("ole32.dll",EntryPoint="PropVariantClear")]static extern int ZwolnijWartosc(ref WartoscWlasciwosci wartosc);
    [DllImport("gdi32.dll",EntryPoint="DeleteObject")]static extern bool ZwolnijBitmapę(IntPtr bitmapa);
    [DllImport("ole32.dll",EntryPoint="CoInitializeEx")]static extern int UruchomCom(IntPtr zarezerwowane,uint tryb);
    [DllImport("ole32.dll",EntryPoint="CoUninitialize")]static extern void ZakonczCom();
    [DllImport("propsys.dll",EntryPoint="PSFormatForDisplayAlloc",CharSet=CharSet.Unicode)]static extern int FormatujWlasciwosc(ref KluczWlasciwosci klucz,ref WartoscWlasciwosci wartosc,int flagi,out IntPtr tekst);
    public static System.Collections.Generic.Dictionary<string,string> PobierzRozszerzone(string sciezka){
        var wynik=new System.Collections.Generic.Dictionary<string,string>();MagazynWlasciwosci magazyn=null;int com=UruchomCom(IntPtr.Zero,0);
        try{Guid id=typeof(MagazynWlasciwosci).GUID;if(OtworzWlasciwosci(sciezka,IntPtr.Zero,0x40,ref id,out magazyn)<0||magazyn==null)return wynik;
            string[,] pola={
                {"System.Image.HorizontalSize","Szerokość"},{"System.Image.VerticalSize","Wysokość"},{"System.Image.BitDepth","Głębia kolorów"},{"System.Photo.Orientation","Orientacja"},{"System.Image.ColorSpace","Przestrzeń kolorów"},
                {"System.Photo.CameraManufacturer","EXIF · Producent aparatu"},{"System.Photo.CameraModel","EXIF · Model aparatu"},{"System.Photo.DateTaken","EXIF · Data wykonania"},{"System.Photo.FocalLength","EXIF · Ogniskowa"},{"System.Photo.ISOSpeed","EXIF · ISO"},{"System.Photo.ExposureTime","EXIF · Czas ekspozycji"},{"System.Photo.FNumber","EXIF · Przysłona"},
                {"System.Media.Duration","Czas trwania"},{"System.Video.FrameWidth","Szerokość filmu"},{"System.Video.FrameHeight","Wysokość filmu"},{"System.Video.FrameRate","FPS"},{"System.Video.EncodingBitrate","Bitrate wideo"},{"System.Video.Compression","Kodek wideo"},{"System.Audio.Compression","Kodek audio"},{"System.Audio.EncodingBitrate","Bitrate audio"},{"System.Audio.SampleRate","Sample rate"},{"System.Audio.ChannelCount","Kanały audio"},
                {"System.Title","Tytuł"},{"System.Music.Artist","Wykonawca"},{"System.Music.AlbumTitle","Album"},{"System.Media.Year","Rok"},{"System.Author","Autor"},{"System.Document.PageCount","Liczba stron"},{"System.Document.SlideCount","Liczba slajdów"},{"System.Document.DateCreated","Data utworzenia dokumentu"}
            };
            for(int i=0;i<pola.GetLength(0);i++){KluczWlasciwosci klucz;if(KluczZNazwy(pola[i,0],out klucz)<0)continue;WartoscWlasciwosci wartosc=new WartoscWlasciwosci();IntPtr tekst=IntPtr.Zero;try{if(magazyn.PobierzWartosc(ref klucz,out wartosc)<0||wartosc.Typ==0)continue;if(FormatujWlasciwosc(ref klucz,ref wartosc,0,out tekst)>=0&&tekst!=IntPtr.Zero){string tresc=Marshal.PtrToStringUni(tekst);if(!String.IsNullOrWhiteSpace(tresc))wynik[pola[i,1]]=tresc;}}finally{if(tekst!=IntPtr.Zero)Marshal.FreeCoTaskMem(tekst);ZwolnijWartosc(ref wartosc);}}
        }catch(COMException){}finally{if(magazyn!=null)Marshal.ReleaseComObject(magazyn);if(com>=0)ZakonczCom();}return wynik;
    }
    static ulong Liczba(MagazynWlasciwosci magazyn,string nazwa){KluczWlasciwosci klucz;if(KluczZNazwy(nazwa,out klucz)<0)return 0;WartoscWlasciwosci wartosc=new WartoscWlasciwosci();try{if(magazyn.PobierzWartosc(ref klucz,out wartosc)<0)return 0;return wartosc.Typ==21?wartosc.Liczba:wartosc.Typ==19?wartosc.MalaLiczba:wartosc.Typ==3&&wartosc.Calkowita>0?(ulong)wartosc.Calkowita:wartosc.Typ==20&&wartosc.Dluga>0?(ulong)wartosc.Dluga:0;}finally{ZwolnijWartosc(ref wartosc);}}
    public static void Uzupelnij(Wiersz plik,MetadaneKafelka dane){string typ=Kafelki.Kategoria(plik);MagazynWlasciwosci magazyn=null;FabrykaObrazow fabryka=null;IntPtr bitmapa=IntPtr.Zero;int com=UruchomCom(IntPtr.Zero,0);
        try{Guid id=typeof(MagazynWlasciwosci).GUID;
            // GPS_BESTEFFORT | GPS_FASTPROPERTIESONLY, bez GPS_READWRITE.
            if(OtworzWlasciwosci(plik.Sciezka,IntPtr.Zero,0x48,ref id,out magazyn)>=0&&magazyn!=null){
                if(typ=="Film"||typ=="Audio"){ulong czas=Liczba(magazyn,"System.Media.Duration");if(czas>0&&czas<=long.MaxValue)dane.Czas=TimeSpan.FromTicks((long)czas);}
                if(typ=="Film"){dane.Klatki=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Video.FrameRate"));dane.Bitrate=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Video.EncodingBitrate"));dane.Szerokosc=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Video.FrameWidth"));dane.Wysokosc=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Video.FrameHeight"));}
                if(typ=="Audio"){dane.Bitrate=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Audio.EncodingBitrate"));dane.Bity=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Audio.SampleSize"));dane.Czestotliwosc=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Audio.SampleRate"));}
                if(typ=="Dokument")dane.Strony=(int)Math.Min(int.MaxValue,Liczba(magazyn,"System.Document.PageCount"));
            }
            if(typ=="Film"){id=typeof(FabrykaObrazow).GUID;if(UtworzElement(plik.Sciezka,IntPtr.Zero,ref id,out fabryka)>=0&&fabryka!=null&&fabryka.PobierzObraz(new RozmiarObrazu {Szerokosc=256,Wysokosc=256},0x18,out bitmapa)>=0&&bitmapa!=IntPtr.Zero){var obraz=Imaging.CreateBitmapSourceFromHBitmap(bitmapa,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());obraz.Freeze();dane.Miniatura=obraz;}}
        }catch(COMException){}finally{if(bitmapa!=IntPtr.Zero)ZwolnijBitmapę(bitmapa);if(fabryka!=null)Marshal.ReleaseComObject(fabryka);if(magazyn!=null)Marshal.ReleaseComObject(magazyn);if(com>=0)ZakonczCom();}
    }
}
}
