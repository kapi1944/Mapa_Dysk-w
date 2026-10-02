using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace MapaDyskow {
public static class Skaner {
    internal static long LiczbaHashowan;
    internal static long LiczbaSkanow;
    [StructLayout(LayoutKind.Sequential)] struct IdPliku {public ulong Wolumin;public ulong Dolna;public ulong Gorna;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string nazwa,uint dostep,uint wspoldzielenie,IntPtr zabezpieczenia,uint utworzenie,uint flagi,IntPtr szablon);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle uchwyt,int klasa,out IdPliku dane,uint rozmiar);
    static readonly HashSet<string> Systemowe=new HashSet<string>(new[]{"windows","program files","program files (x86)","programdata","appdata","recovery","system volume information","$recycle.bin","$windows.~ws","$windows.~bt","$getcurrent","esd","perflogs","msocache","config.msi"},StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string,int> Typy=ZbudujTypy();
    static Dictionary<string,int> ZbudujTypy(){var typy=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);string[] listy={"mp4 mov avi mkv mts m2ts mpg mpeg m4v webm 3gp wmv vob ts insv","jpg jpeg png heic heif gif webp bmp tif tiff raw arw cr2 cr3 nef dng raf rw2","pdf doc docx odt txt rtf xls xlsx ods csv ppt pptx odp md pages numbers epub","zip rar 7z tar gz bz2 xz tgz iso img"};for(int i=0;i<4;i++)foreach(string rozszerzenie in listy[i].Split(' '))typy[rozszerzenie]=i;return typy;}
    sealed class StanFolderu {public string Sciezka,Rodzic;public long Rozmiar,Pliki,Podfoldery,Duplikaty;public double Zmiana;public long[] Typy=new long[5];public bool Systemowy;public string Uwagi="";}
    static bool WZakresie(string sciezka,string korzen){return sciezka.Equals(korzen,StringComparison.OrdinalIgnoreCase)||sciezka.StartsWith(korzen.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
    static bool CzySystemowy(string sciezka){return sciezka.Split('\\').Any(nazwa=>Systemowe.Contains(nazwa));}
    static string[] Tozsamosc(string sciezka){using(var uchwyt=CreateFile(sciezka,0,7,IntPtr.Zero,3,0x80,IntPtr.Zero)){IdPliku dane;if(uchwyt.IsInvalid||!GetFileInformationByHandleEx(uchwyt,18,out dane,24))return new[]{"0","0"};BigInteger ident=((BigInteger)dane.Gorna<<64)+dane.Dolna;return new[]{dane.Wolumin.ToString(CultureInfo.InvariantCulture),ident.ToString(CultureInfo.InvariantCulture)};}}
    static string ObliczSkrot(string sciezka,CancellationToken token){LiczbaHashowan++;if(!Regex.IsMatch(sciezka,@"^[a-zA-Z]:\\"))throw new IOException("Wymagana jest lokalna ścieżka pliku.");using(var skrot=SHA256.Create())using(var plik=new FileStream(sciezka,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,1024*1024,FileOptions.SequentialScan)){byte[] bufor=new byte[1024*1024];int odczyt;while((odczyt=plik.Read(bufor,0,bufor.Length))>0){token.ThrowIfCancellationRequested();skrot.TransformBlock(bufor,0,odczyt,null,0);}skrot.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(skrot.Hash).Replace("-","").ToLowerInvariant();}}
    static void Zdarzenie(Baza baza,string sciezka,string rodzaj,string opis){baza.Wykonaj("INSERT INTO zdarzenia VALUES(?,?,?)",sciezka,rodzaj,opis);}
    public static string Skanuj(string podanyKorzen,bool hashe,CancellationToken token,Action<string> postep){
        Interlocked.Increment(ref LiczbaSkanow);
        string korzen=Path.GetFullPath(podanyKorzen);if(!Regex.IsMatch(korzen,@"^[a-zA-Z]:\\"))throw new IOException("Obsługiwane są lokalne ścieżki dysków Windows.");if(korzen.Length>3)korzen=korzen.TrimEnd('\\');if(!Directory.Exists(korzen))throw new IOException("Folder nie istnieje lub dysk jest niedostępny.");
        if(WZakresie(korzen,Program.Katalog.TrimEnd('\\'))||korzen.Equals(Program.KatalogAudytu.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new IOException("Katalog aplikacji i wyników audytu jest wyłączony, aby nie indeksować własnej bazy.");
        if(((int)File.GetAttributes(korzen)&(0x400|0x1000|0x40000|0x400000))!=0)throw new IOException("Nie skanuję dowiązań ani folderów wymagających pobrania z chmury.");
        double czas=(DateTime.UtcNow-Program.Epoka).TotalSeconds;long licznik=0,hashowane=0,bledyHasha=0;int pominiete=0;var foldery=new Dictionary<string,StanFolderu>(StringComparer.OrdinalIgnoreCase);var stos=new Stack<string>();stos.Push(korzen);
        using(var baza=new Baza(Program.Indeks,true)){
            baza.Wykonaj("PRAGMA journal_mode=WAL");baza.Wykonaj("CREATE TEMP TABLE nowe_pliki(sciezka TEXT PRIMARY KEY,folder TEXT,rozmiar INTEGER,zmiana REAL,typ TEXT,atrybuty INTEGER)");
            baza.Wykonaj("BEGIN IMMEDIATE");bool zatwierdzone=false;
            try {
                while(stos.Count>0){token.ThrowIfCancellationRequested();string sciezka=stos.Pop();var katalog=new DirectoryInfo(sciezka);string rodzic=sciezka.Equals(korzen,StringComparison.OrdinalIgnoreCase)?Path.GetDirectoryName(sciezka.TrimEnd('\\')):katalog.Parent.FullName;if(rodzic!=null&&rodzic.EndsWith(":"))rodzic+="\\";if(sciezka.Length==3)rodzic=null;
                    var stan=new StanFolderu {Sciezka=sciezka,Rodzic=rodzic,Zmiana=(katalog.LastWriteTimeUtc-Program.Epoka).TotalSeconds,Systemowy=CzySystemowy(sciezka)};foldery[sciezka]=stan;
                    FileSystemInfo[] wpisy;
                    try{wpisy=katalog.GetFileSystemInfos();}catch(Exception blad){throw new IOException("Niepełny odczyt "+sciezka+". Poprzedni indeks tego zakresu zachowano. "+blad.Message,blad);}
                    foreach(var wpis in wpisy){token.ThrowIfCancellationRequested();int atrybuty=(int)wpis.Attributes;bool jestFolder=(atrybuty&(int)FileAttributes.Directory)!=0;
                        if((atrybuty&(0x400|0x1000|0x40000|0x400000))!=0){Zdarzenie(baza,wpis.FullName,"pominiety_link_lub_chmura","Skan aplikacji: bez przechodzenia przez linki i bez pobierania plików offline.");pominiete++;continue;}
                        if(jestFolder){if(Systemowe.Contains(wpis.Name)||wpis.FullName.Equals(Program.Katalog.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)||wpis.FullName.Equals(Program.KatalogAudytu.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)){
                                Zdarzenie(baza,wpis.FullName,"niskopriorytetowy_pominiety","Skan aplikacji: zakres niskopriorytetowy.");pominiete++;continue;}
                            stos.Push(wpis.FullName);continue;}
                        var plik=wpis as FileInfo;if(plik==null)continue;long rozmiar=plik.Length;double zmiana=(plik.LastWriteTimeUtc-Program.Epoka).TotalSeconds;string rozszerzenie=plik.Extension.TrimStart('.');int typ=Typy.ContainsKey(rozszerzenie)?Typy[rozszerzenie]:4;string nazwaTypu=new[]{"filmy","zdjecia","dokumenty","archiwa","inne"}[typ];
                        baza.Wykonaj("INSERT INTO nowe_pliki VALUES(?,?,?,?,?,?)",plik.FullName,sciezka,rozmiar,zmiana,nazwaTypu,atrybuty);stan.Rozmiar+=rozmiar;stan.Pliki++;stan.Typy[typ]+=rozmiar;stan.Zmiana=Math.Max(stan.Zmiana,zmiana);licznik++;
                        if(licznik%1000==0)postep(korzen+" • odczytano "+licznik.ToString("N0")+" plików");
                    }
                }
                token.ThrowIfCancellationRequested();
                // Zastępujemy wyłącznie rekordy własnego indeksu, nigdy pliki na dyskach.
                baza.Wykonaj("DELETE FROM pliki WHERE (sciezka=? COLLATE NOCASE OR sciezka LIKE ? ESCAPE '^') AND sciezka NOT IN(SELECT sciezka FROM nowe_pliki) AND NOT EXISTS(SELECT 1 FROM zdarzenia z WHERE z.rodzaj IN ('niskopriorytetowy_pominiety','pominiety_link_lub_chmura') AND (pliki.sciezka=z.sciezka COLLATE NOCASE OR substr(pliki.sciezka,1,length(z.sciezka)+1)=z.sciezka||char(92)))",korzen,Program.Prefiks(korzen));
                baza.Wykonaj("UPDATE pliki SET sha256=NULL,probka=NULL WHERE sciezka IN(SELECT n.sciezka FROM nowe_pliki n WHERE n.sciezka=pliki.sciezka AND (n.rozmiar<>pliki.rozmiar OR abs(n.zmiana-pliki.zmiana)>0.001))");
                baza.Wykonaj("INSERT OR IGNORE INTO pliki(sciezka,folder,rozmiar,zmiana,typ,atrybuty,urzadzenie,tozsamosc) SELECT sciezka,folder,rozmiar,zmiana,typ,atrybuty,'0','0' FROM nowe_pliki");
                baza.Wykonaj("UPDATE pliki SET folder=(SELECT folder FROM nowe_pliki n WHERE n.sciezka=pliki.sciezka),rozmiar=(SELECT rozmiar FROM nowe_pliki n WHERE n.sciezka=pliki.sciezka),zmiana=(SELECT zmiana FROM nowe_pliki n WHERE n.sciezka=pliki.sciezka),typ=(SELECT typ FROM nowe_pliki n WHERE n.sciezka=pliki.sciezka),atrybuty=(SELECT atrybuty FROM nowe_pliki n WHERE n.sciezka=pliki.sciezka) WHERE sciezka IN(SELECT sciezka FROM nowe_pliki)");
                if(hashe){baza.Wykonaj("CREATE TEMP TABLE rozmiary_do_hash AS SELECT DISTINCT p.rozmiar FROM pliki p JOIN nowe_pliki n ON p.sciezka=n.sciezka WHERE p.sha256 IS NULL AND p.rozmiar>0 AND (SELECT count(*) FROM pliki q WHERE q.rozmiar=p.rozmiar)>1");postep(korzen+" • potwierdzanie nowych kandydatów SHA-256");long ostatni=0;while(true){token.ThrowIfCancellationRequested();var kandydaci=baza.Zapytaj("SELECT p.id,p.sciezka,p.rozmiar,p.zmiana FROM pliki p WHERE p.id>? AND p.sha256 IS NULL AND p.rozmiar>0 AND p.rozmiar IN(SELECT rozmiar FROM rozmiary_do_hash) ORDER BY p.id LIMIT 100",ostatni);if(kandydaci.Count==0)break;foreach(var r in kandydaci){token.ThrowIfCancellationRequested();ostatni=Convert.ToInt64(r[0]);string sciezka=(string)r[1];try{var przed=new FileInfo(sciezka);long rozmiar=przed.Length;long data=przed.LastWriteTimeUtc.Ticks;if(Math.Abs((przed.LastWriteTimeUtc-Program.Epoka).TotalSeconds-Convert.ToDouble(r[3]))>0.001||((int)przed.Attributes&(0x400|0x1000|0x40000|0x400000))!=0)throw new IOException("Metadane zmieniły się lub plik wymaga pobrania — odśwież jego folder.");string skrot=ObliczSkrot(sciezka,token);var po=new FileInfo(sciezka);if(po.Length!=rozmiar||po.LastWriteTimeUtc.Ticks!=data||rozmiar!=Convert.ToInt64(r[2]))throw new IOException("Plik zmienił się podczas odczytu.");var tozsamosc=Tozsamosc(sciezka);baza.Wykonaj("UPDATE pliki SET sha256=?,urzadzenie=?,tozsamosc=? WHERE id=?",skrot,tozsamosc[0],tozsamosc[1],ostatni);hashowane++;postep(korzen+" • SHA-256: "+hashowane.ToString("N0")+" kandydatów");}catch(OperationCanceledException){throw;}catch(Exception blad){bledyHasha++;Zdarzenie(baza,sciezka,"blad_hash",blad.Message);}}}}
                if(bledyHasha>0)foldery[korzen].Uwagi="Niepotwierdzone hashe: "+bledyHasha+". Szczegóły w zdarzeniach indeksu.";UpewnijTozsamosci(baza,token);OdbudujDuplikaty(baza);OdbudujFoldery(baza,foldery,korzen,czas);OdbudujOznaczeniaDuplikatow(baza);
                token.ThrowIfCancellationRequested();baza.Wykonaj("COMMIT");zatwierdzone=true;
                try{var dysk=new DriveInfo(Path.GetPathRoot(korzen));baza.Wykonaj("INSERT OR REPLACE INTO dyski VALUES(?,?,?,?)",dysk.Name,dysk.TotalSize,dysk.TotalSize-dysk.AvailableFreeSpace,dysk.AvailableFreeSpace);}catch(IOException){}
                return korzen+" • gotowe: "+licznik.ToString("N0")+" plików, "+hashowane.ToString("N0")+" nowych hashów, "+pominiete+" pominięć, "+bledyHasha+" błędów hashów";
            }finally{if(!zatwierdzone)try{baza.Wykonaj("ROLLBACK");}catch{} }
        }
    }
    static void UpewnijTozsamosci(Baza baza,CancellationToken token){
        foreach(var r in baza.Zapytaj("SELECT p.id,p.sciezka,p.rozmiar,p.zmiana FROM pliki p WHERE p.sha256 IS NOT NULL AND (p.urzadzenie='0' OR p.tozsamosc='0') AND (SELECT count(*) FROM pliki q WHERE q.rozmiar=p.rozmiar AND q.sha256=p.sha256)>1")){
            token.ThrowIfCancellationRequested();string sciezka=(string)r[1];if(!Regex.IsMatch(sciezka,@"^[a-zA-Z]:\\"))continue;
            try{var plik=new FileInfo(sciezka);if(plik.Length!=Convert.ToInt64(r[2])||Math.Abs((plik.LastWriteTimeUtc-Program.Epoka).TotalSeconds-Convert.ToDouble(r[3]))>0.001||((int)plik.Attributes&(0x400|0x1000|0x40000|0x400000))!=0)continue;var dane=Tozsamosc(sciezka);baza.Wykonaj("UPDATE pliki SET urzadzenie=?,tozsamosc=? WHERE id=?",dane[0],dane[1],r[0]);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    internal static void OdbudujDuplikaty(Baza baza){
        baza.Wykonaj("DELETE FROM lokalizacje_duplikatow");baza.Wykonaj("DELETE FROM grupy_duplikatow");
        string klucz="CASE WHEN urzadzenie IS NULL OR tozsamosc IS NULL OR urzadzenie='0' OR tozsamosc='0' THEN sciezka ELSE urzadzenie||':'||tozsamosc END";
        baza.Wykonaj("INSERT INTO grupy_duplikatow(sha256,rozmiar,lokalizacje,niezalezne,zajete,potencjal,niepewne) SELECT sha256,rozmiar,count(*),count(DISTINCT "+klucz+"),count(DISTINCT "+klucz+")*rozmiar,(count(DISTINCT "+klucz+")-1)*rozmiar,sum(CASE WHEN urzadzenie='0' OR tozsamosc='0' THEN 1 ELSE 0 END) FROM pliki WHERE sha256 IS NOT NULL GROUP BY rozmiar,sha256 HAVING count(*)>1");
        baza.Wykonaj("INSERT INTO lokalizacje_duplikatow SELECT g.id,p.sciezka,p.folder FROM pliki p JOIN grupy_duplikatow g ON p.rozmiar=g.rozmiar AND p.sha256=g.sha256");
    }
    internal static void OdbudujOznaczeniaDuplikatow(Baza baza){
        var rodzice=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var sumy=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        foreach(var r in baza.Zapytaj("SELECT sciezka,rodzic FROM widok_folderow")){rodzice[(string)r[0]]=Convert.ToString(r[1]);sumy[(string)r[0]]=0;}
        foreach(var r in baza.Zapytaj("SELECT l.folder,sum(g.rozmiar) FROM lokalizacje_duplikatow l JOIN grupy_duplikatow g ON l.grupa=g.id GROUP BY l.folder"))if(sumy.ContainsKey((string)r[0]))sumy[(string)r[0]]=Convert.ToInt64(r[1]);
        foreach(string sciezka in sumy.Keys.OrderByDescending(s=>s.Count(c=>c=='\\')).ToList()){string rodzic=rodzice[sciezka];if(!String.IsNullOrEmpty(rodzic)&&sumy.ContainsKey(rodzic))sumy[rodzic]+=sumy[sciezka];}
        baza.Wykonaj("UPDATE widok_folderow SET duplikaty=0");foreach(var para in sumy)if(para.Value>0)baza.Wykonaj("UPDATE widok_folderow SET duplikaty=? WHERE sciezka=?",para.Value,para.Key);
    }
    static void OdbudujFoldery(Baza baza,Dictionary<string,StanFolderu> stany,string korzen,double czas){
        // Agregacja ogranicza pamięć do folderów; pliki są grupowane przez SQLite.
        foreach(var r in baza.Zapytaj("SELECT p.folder,sum(p.rozmiar),count(*),max(p.zmiana),sum(CASE WHEN typ='filmy' THEN rozmiar ELSE 0 END),sum(CASE WHEN typ='zdjecia' THEN rozmiar ELSE 0 END),sum(CASE WHEN typ='dokumenty' THEN rozmiar ELSE 0 END),sum(CASE WHEN typ='archiwa' THEN rozmiar ELSE 0 END),sum(CASE WHEN typ='inne' THEN rozmiar ELSE 0 END) FROM pliki p WHERE p.sciezka=? COLLATE NOCASE OR p.sciezka LIKE ? ESCAPE '^' GROUP BY p.folder",korzen,Program.Prefiks(korzen))){string f=(string)r[0];if(!stany.ContainsKey(f)){string rodzic=Path.GetDirectoryName(f);stany[f]=new StanFolderu {Sciezka=f,Rodzic=rodzic,Uwagi="Dane zachowane z pominiętego zakresu — nie odświeżono",Systemowy=CzySystemowy(f)};}var stan=stany[f];stan.Rozmiar=Convert.ToInt64(r[1]);stan.Pliki=Convert.ToInt64(r[2]);stan.Zmiana=Math.Max(stan.Zmiana,Convert.ToDouble(r[3]));for(int i=0;i<5;i++)stan.Typy[i]=Convert.ToInt64(r[i+4]);}
        foreach(var r in baza.Zapytaj("SELECT l.folder,sum(g.rozmiar) FROM lokalizacje_duplikatow l JOIN grupy_duplikatow g ON l.grupa=g.id WHERE l.sciezka=? COLLATE NOCASE OR l.sciezka LIKE ? ESCAPE '^' GROUP BY l.folder",korzen,Program.Prefiks(korzen)))if(stany.ContainsKey((string)r[0]))stany[(string)r[0]].Duplikaty=Convert.ToInt64(r[1]);
        // Zachowane podfoldery potrzebują całego łańcucha przodków.
        foreach(string sciezka in stany.Keys.ToList()){string f=sciezka;while(!f.Equals(korzen,StringComparison.OrdinalIgnoreCase)){string rodzic=Path.GetDirectoryName(f.TrimEnd('\\'));if(String.IsNullOrEmpty(rodzic))break;if(rodzic.EndsWith(":"))rodzic+="\\";if(!WZakresie(rodzic,korzen))break;if(!stany.ContainsKey(rodzic))stany[rodzic]=new StanFolderu {Sciezka=rodzic,Rodzic=Path.GetDirectoryName(rodzic.TrimEnd('\\')),Uwagi="Zakres zachowany z wcześniejszego indeksu"};f=rodzic;}}
        foreach(var stan in stany.Values.OrderByDescending(w=>w.Sciezka.Count(c=>c=='\\')).ToList()){if(stan.Sciezka.Equals(korzen,StringComparison.OrdinalIgnoreCase)||stan.Rodzic==null||!stany.ContainsKey(stan.Rodzic))continue;var rodzic=stany[stan.Rodzic];rodzic.Rozmiar+=stan.Rozmiar;rodzic.Pliki+=stan.Pliki;rodzic.Podfoldery+=stan.Podfoldery+1;rodzic.Zmiana=Math.Max(rodzic.Zmiana,stan.Zmiana);rodzic.Duplikaty+=stan.Duplikaty;for(int i=0;i<5;i++)rodzic.Typy[i]+=stan.Typy[i];}
        var stary=baza.Zapytaj("SELECT rozmiar,pliki,podfoldery,filmy,zdjecia,dokumenty,archiwa,inne,duplikaty FROM widok_folderow WHERE sciezka=?",korzen);long[] poprzednie=new long[9];if(stary.Count>0&&Convert.ToInt64(stary[0][0])>=0)for(int i=0;i<9;i++)poprzednie[i]=Convert.ToInt64(stary[0][i]);
        baza.Wykonaj("DELETE FROM widok_folderow WHERE (sciezka=? COLLATE NOCASE OR sciezka LIKE ? ESCAPE '^') AND systemowy=0",korzen,Program.Prefiks(korzen));
        foreach(var stan in stany.Values){bool sygnal=Regex.IsMatch(Path.GetFileName(stan.Sciezka.TrimEnd('\\')),@"zgr|zrzut|backup|kopia|zapasow|stare|archiw|dcim|camera|pobrane|downloads|dysk_[kwy]|tymczas",RegexOptions.IgnoreCase);baza.Wykonaj("INSERT OR REPLACE INTO widok_folderow VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",stan.Sciezka,stan.Rodzic,Path.GetFileName(stan.Sciezka.TrimEnd('\\')),stan.Rozmiar,stan.Pliki,stan.Podfoldery,stan.Zmiana,stan.Typy[0],stan.Typy[1],stan.Typy[2],stan.Typy[3],stan.Typy[4],stan.Duplikaty,sygnal?1:0,stan.Systemowy?1:0,stan.Uwagi,czas);}
        var nowy=stany[korzen];long[] aktualne={nowy.Rozmiar,nowy.Pliki,nowy.Podfoldery,nowy.Typy[0],nowy.Typy[1],nowy.Typy[2],nowy.Typy[3],nowy.Typy[4],nowy.Duplikaty};string przodek=nowy.Rodzic;long dodatkowyFolder=stary.Count==0||Convert.ToInt64(stary[0][0])<0?1:0;
        while(!String.IsNullOrEmpty(przodek)){var w=baza.Zapytaj("SELECT rodzic,zmiana,rozmiar FROM widok_folderow WHERE sciezka=?",przodek);if(w.Count==0)break;
double dataPrzodka=Convert.ToDouble(baza.Wartosc("SELECT max(coalesce((SELECT max(zmiana) FROM pliki WHERE folder=?),0),coalesce((SELECT max(zmiana) FROM widok_folderow WHERE rodzic=?),0))",przodek,przodek));
try{var katalogPrzodka=new DirectoryInfo(przodek);if(katalogPrzodka.Exists)dataPrzodka=Math.Max(dataPrzodka,(katalogPrzodka.LastWriteTimeUtc-Program.Epoka).TotalSeconds);}catch(IOException){dataPrzodka=Math.Max(dataPrzodka,Convert.ToDouble(w[0][1]));}
if(Convert.ToInt64(w[0][2])<0){baza.Wykonaj("UPDATE widok_folderow SET uwagi='Odświeżono tylko podfolder — cały zakres nadal nieprzeskanowany' WHERE sciezka=?",przodek);przodek=Convert.ToString(w[0][0]);continue;}
baza.Wykonaj("UPDATE widok_folderow SET rozmiar=rozmiar+?,pliki=pliki+?,podfoldery=podfoldery+?,filmy=filmy+?,zdjecia=zdjecia+?,dokumenty=dokumenty+?,archiwa=archiwa+?,inne=inne+?,duplikaty=duplikaty+?,zmiana=?,ostatni_skan=? WHERE sciezka=?",aktualne[0]-poprzednie[0],aktualne[1]-poprzednie[1],aktualne[2]-poprzednie[2]+dodatkowyFolder,aktualne[3]-poprzednie[3],aktualne[4]-poprzednie[4],aktualne[5]-poprzednie[5],aktualne[6]-poprzednie[6],aktualne[7]-poprzednie[7],aktualne[8]-poprzednie[8],dataPrzodka,czas,przodek);przodek=Convert.ToString(w[0][0]);}
    }
}
}
