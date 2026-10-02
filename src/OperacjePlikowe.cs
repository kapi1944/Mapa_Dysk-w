using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MapaDyskow {
public static class OperacjePlikowe {
    internal static event Action<string,string> ZmienionoNazwe;
    [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool UtworzKatalog(string sciezka,IntPtr zabezpieczenia);
    [DllImport("kernel32.dll",EntryPoint="MoveFileExW",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ZmienNazweWindows(string stara,string nowa,uint flagi);
    public static string Waliduj(string nazwa){
        if(String.IsNullOrWhiteSpace(nazwa))return "Wpisz nazwę.";
        if(nazwa.Length>255)return "Nazwa jest zbyt długa (maksymalnie 255 znaków).";
        if(nazwa.IndexOfAny(Path.GetInvalidFileNameChars())>=0)return "Nazwa zawiera niedozwolony znak: \\ / : * ? \" < > | lub znak sterujący.";
        if(nazwa.EndsWith(" ")||nazwa.EndsWith("."))return "Nazwa nie może kończyć się spacją ani kropką.";
        if(Regex.IsMatch(nazwa.Split('.')[0].TrimEnd(' '),@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$",RegexOptions.IgnoreCase))return "Ta nazwa jest zarezerwowana przez Windows.";
        return "";
    }
    public static string Podmien(string sciezka,string stara,string nowa){if(String.IsNullOrEmpty(sciezka))return sciezka;if(sciezka.Equals(stara,StringComparison.OrdinalIgnoreCase))return nowa;return sciezka.StartsWith(stara.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)?nowa+sciezka.Substring(stara.Length):sciezka;}
    public static bool ZmieniaRozszerzenie(string stara,string nowa){return !Path.GetExtension(stara).Equals(Path.GetExtension(nowa),StringComparison.OrdinalIgnoreCase);}
    public static int DlugoscZaznaczenia(string nazwa,bool plik){int dlugosc=plik?nazwa.Length-Path.GetExtension(nazwa).Length:nazwa.Length;return dlugosc>0?dlugosc:nazwa.Length;}
    static void SprawdzFolder(string folder){if(!Path.IsPathRooted(folder)||!Directory.Exists(folder))throw new DirectoryNotFoundException();}
    static void BladWindows(){int kod=Marshal.GetLastWin32Error();if(kod==5)throw new UnauthorizedAccessException();if(kod==206)throw new PathTooLongException();if(kod==183||kod==80)throw new IOException("Element o tej nazwie już istnieje.");throw new IOException(new Win32Exception(kod).Message);}
    public static string Komunikat(Exception blad){if(blad is UnauthorizedAccessException)return "Windows odmówił dostępu do tej lokalizacji.";if(blad is PathTooLongException)return "Ścieżka lub nazwa jest zbyt długa.";if(blad is DirectoryNotFoundException||blad is DriveNotFoundException)return "Folder nie istnieje lub dysk jest niedostępny.";if(blad is ArgumentException||blad is NotSupportedException)return "Ścieżka lub nazwa jest nieprawidłowa.";return blad is IOException?"Operacja nie powiodła się. "+blad.Message:"Nie udało się zaktualizować indeksu. Odśwież folder klawiszem F5.";}
    public static string NowyFolder(string folder){
        SprawdzFolder(folder);string nowa;
        for(int numer=1;;numer++){nowa=Path.Combine(folder,numer==1?"Nowy folder":"Nowy folder ("+numer+")");if(UtworzKatalog(nowa,IntPtr.Zero))break;int kod=Marshal.GetLastWin32Error();if(kod!=183&&kod!=80)BladWindows();}
        try{using(var baza=new Baza(Program.Indeks,true)){baza.Wykonaj("BEGIN IMMEDIATE");try{DodajFolder(baza,new DirectoryInfo(nowa),true);PrzeliczRodzicow(baza,folder);baza.Wykonaj("COMMIT");}catch{baza.Wykonaj("ROLLBACK");throw;}}}catch(Exception blad){throw new IOException("Folder utworzono: "+nowa+". Indeks nie został zaktualizowany; użyj F5. "+Komunikat(blad));}return nowa;
    }
    public static string ZmienNazwe(string stara,string nazwa,bool jestFolder){
        if(((File.GetAttributes(stara)&FileAttributes.Directory)!=0)!=jestFolder)throw new IOException("Rodzaj elementu zmienił się. Odśwież folder klawiszem F5.");string blad=Waliduj(nazwa);if(blad!="")throw new ArgumentException(blad);string rodzic=Path.GetDirectoryName(stara.TrimEnd('\\'));SprawdzFolder(rodzic);string nowa=Path.Combine(rodzic,nazwa);if(stara==nowa)return stara;
        if(!stara.Equals(nowa,StringComparison.OrdinalIgnoreCase)&&(File.Exists(nowa)||Directory.Exists(nowa)))throw new IOException("Element o tej nazwie już istnieje.");
        using(var baza=new Baza(Program.Indeks,true)){
            baza.Wykonaj("BEGIN IMMEDIATE");bool zmieniono=false;
            try{
                if(!stara.Equals(nowa,StringComparison.OrdinalIgnoreCase)&&(Convert.ToInt64(baza.Wartosc("SELECT count(*) FROM pliki WHERE sciezka=? COLLATE NOCASE",nowa))+Convert.ToInt64(baza.Wartosc("SELECT count(*) FROM widok_folderow WHERE sciezka=? COLLATE NOCASE",nowa)))>0)throw new IOException("Element o tej nazwie już istnieje w indeksie. Odśwież folder klawiszem F5.");
                // Flagi 0: Windows nie zastępuje celu; obie ścieżki mają tego samego rodzica.
                if(!ZmienNazweWindows(stara,nowa,0))BladWindows();zmieniono=true;
                foreach(string tabela in new[]{"pliki","widok_folderow","lokalizacje_duplikatow","zdarzenia"}){
                    string kolumna=tabela=="widok_folderow"?"rodzic":(tabela=="pliki"||tabela=="lokalizacje_duplikatow"?"folder":null);
                    string warunek="sciezka=? COLLATE NOCASE"+(jestFolder?" OR sciezka LIKE ? ESCAPE '^'":"");object[] argumenty=jestFolder?new object[]{stara,Program.Prefiks(stara)}:new object[]{stara};
                    foreach(var r in baza.Zapytaj("SELECT sciezka"+(kolumna==null?"":","+kolumna)+" FROM "+tabela+" WHERE "+warunek,argumenty)){
                        string poprzednia=(string)r[0],aktualna=Podmien(poprzednia,stara,nowa);
                        baza.Wykonaj("UPDATE "+tabela+" SET sciezka=?"+(kolumna==null?"":","+kolumna+"=?")+" WHERE sciezka=?",kolumna==null?new object[]{aktualna,poprzednia}:new object[]{aktualna,Podmien(Convert.ToString(r[1]),stara,nowa),poprzednia});
                    }
                }
                if(jestFolder)baza.Wykonaj("UPDATE widok_folderow SET nazwa=?,sygnal=? WHERE sciezka=?",nazwa,Regex.IsMatch(nazwa,"zgr|zrzut|backup|kopia|zapasow|stare|archiw|dcim|camera|pobrane|downloads|dysk_[kwy]|tymczas",RegexOptions.IgnoreCase)?1:0,nowa);
                else baza.Wykonaj("UPDATE pliki SET typ=? WHERE sciezka=?",Typ(nowa),nowa);
                PrzeliczRodzicow(baza,rodzic);baza.Wykonaj("COMMIT");
            }catch{baza.Wykonaj("ROLLBACK");if(zmieniono&&!ZmienNazweWindows(nowa,stara,0))throw new IOException("Nazwa została zmieniona na "+nowa+", ale zapis indeksu i przywrócenie nazwy nie powiodły się. Użyj F5.");throw;}
        }
        try{Kafelki.ZmienSciezkiCache(stara,nowa);}catch(Exception bladCache){try{File.AppendAllText(Path.Combine(Program.Katalog,"bledy-cache.log"),Komunikat(bladCache)+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}if(ZmienionoNazwe!=null)ZmienionoNazwe(stara,nowa);return nowa;
    }
    static string Typ(string sciezka){string k=Kafelki.Kategoria(new Wiersz {Rodzaj="plik",Sciezka=sciezka});return k=="Film"?"filmy":k=="Zdjęcie"?"zdjecia":k=="Dokument"||k=="Arkusz"?"dokumenty":k=="Archiwum"||k=="Obraz dysku / backup"?"archiwa":"inne";}
    static void DodajFolder(Baza baza,DirectoryInfo dane,bool pusty){string rodzic=dane.Parent==null?null:dane.Parent.FullName;bool systemowy=Regex.IsMatch(dane.FullName,@"(^|\\)(Windows|Program Files(?: \(x86\))?|ProgramData|AppData|Recovery|System Volume Information|\$Recycle.Bin)(\\|$)",RegexOptions.IgnoreCase);baza.Wykonaj("INSERT OR IGNORE INTO widok_folderow VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",dane.FullName,rodzic,dane.Name,pusty?0:-1,0,0,(dane.LastWriteTimeUtc-Program.Epoka).TotalSeconds,0,0,0,0,0,0,0,systemowy?1:0,pusty?"":"Nie przeskanowano podfolderu",0);}
    static void PrzeliczRodzicow(Baza baza,string folder){
        while(!String.IsNullOrEmpty(folder)){
            var dane=baza.Zapytaj("SELECT rodzic,rozmiar FROM widok_folderow WHERE sciezka=?",folder);if(dane.Count==0)break;
            if(Convert.ToInt64(dane[0][1])>=0){baza.Wykonaj("UPDATE widok_folderow SET rozmiar=coalesce((SELECT sum(rozmiar) FROM pliki WHERE folder=?),0)+coalesce((SELECT sum(max(0,rozmiar)) FROM widok_folderow WHERE rodzic=?),0),pliki=(SELECT count(*) FROM pliki WHERE folder=?)+coalesce((SELECT sum(pliki) FROM widok_folderow WHERE rodzic=?),0),podfoldery=coalesce((SELECT sum(podfoldery+1) FROM widok_folderow WHERE rodzic=?),0) WHERE sciezka=?",folder,folder,folder,folder,folder,folder);
                foreach(string typ in new[]{"filmy","zdjecia","dokumenty","archiwa","inne"})baza.Wykonaj("UPDATE widok_folderow SET "+typ+"=coalesce((SELECT sum(rozmiar) FROM pliki WHERE folder=? AND typ=?),0)+coalesce((SELECT sum("+typ+") FROM widok_folderow WHERE rodzic=?),0) WHERE sciezka=?",folder,typ,folder,folder);
            }double data=0;try{data=(Directory.GetLastWriteTimeUtc(folder)-Program.Epoka).TotalSeconds;}catch(IOException){}catch(UnauthorizedAccessException){}baza.Wykonaj("UPDATE widok_folderow SET zmiana=max(?,coalesce((SELECT max(zmiana) FROM pliki WHERE folder=?),0),coalesce((SELECT max(zmiana) FROM widok_folderow WHERE rodzic=?),0)) WHERE sciezka=?",data,folder,folder,folder);folder=Convert.ToString(dane[0][0]);
        }
    }
    public static void OdswiezFolder(string folder){
        SprawdzFolder(folder);var wpisy=new DirectoryInfo(folder).GetFileSystemInfos();
        using(var baza=new Baza(Program.Indeks,true)){baza.Wykonaj("BEGIN IMMEDIATE");try{
            DodajFolder(baza,new DirectoryInfo(folder),false);var obecne=new HashSet<string>(wpisy.Select(w=>w.FullName),StringComparer.OrdinalIgnoreCase);
            foreach(var r in baza.Zapytaj("SELECT sciezka FROM pliki WHERE folder=? UNION SELECT sciezka FROM widok_folderow WHERE rodzic=?",folder,folder))if(!obecne.Contains((string)r[0]))foreach(string tabela in new[]{"pliki","widok_folderow","lokalizacje_duplikatow"})baza.Wykonaj("DELETE FROM "+tabela+" WHERE sciezka=? COLLATE NOCASE OR sciezka LIKE ? ESCAPE '^'",r[0],Program.Prefiks((string)r[0]));
            foreach(var wpis in wpisy){if((wpis.Attributes&FileAttributes.Directory)!=0){DodajFolder(baza,(DirectoryInfo)wpis,false);continue;}var plik=(FileInfo)wpis;double zmiana=(plik.LastWriteTimeUtc-Program.Epoka).TotalSeconds;
                baza.Wykonaj("INSERT OR IGNORE INTO pliki(sciezka,folder,rozmiar,zmiana,typ,atrybuty,urzadzenie,tozsamosc) VALUES(?,?,?,?,?,?,'0','0')",plik.FullName,folder,plik.Length,zmiana,Typ(plik.FullName),(int)plik.Attributes);
                baza.Wykonaj("UPDATE pliki SET sha256=CASE WHEN rozmiar=? AND abs(zmiana-?)<0.001 THEN sha256 ELSE NULL END,probka=CASE WHEN rozmiar=? AND abs(zmiana-?)<0.001 THEN probka ELSE NULL END,rozmiar=?,zmiana=?,typ=?,atrybuty=? WHERE sciezka=?",plik.Length,zmiana,plik.Length,zmiana,plik.Length,zmiana,Typ(plik.FullName),(int)plik.Attributes,plik.FullName);
            }
            Skaner.OdbudujDuplikaty(baza);Skaner.OdbudujOznaczeniaDuplikatow(baza);PrzeliczRodzicow(baza,folder);baza.Wykonaj("COMMIT");
        }catch{baza.Wykonaj("ROLLBACK");throw;}}
    }
}
}
