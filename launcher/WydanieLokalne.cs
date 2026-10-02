using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LauncherMapy {
public sealed class StanWydania {
    public string version,sourceHash,builtAt,exeSha256;
}
public sealed class UstawieniaLaunchera {public bool AutomatycznieBuduj=true;}
public sealed class WynikAktualizacji {public bool Zbudowano;public string Powod,Blad;}
sealed class TransakcjaWydania {public string Kopia;public List<string> Pliki,Poprzednie;}
public sealed class WydanieLokalne {
    public readonly string KatalogRepo,PlikLog;
    static readonly string[] plikiWydania={"MapaDyskow.exe.config","konfiguracja.json","kompilacja.json","stan-wydania.json","MapaDyskow.exe"};
    readonly JavaScriptSerializer serializator=new JavaScriptSerializer();
    public WydanieLokalne(string repo){KatalogRepo=Path.GetFullPath(repo);Directory.CreateDirectory(Path.Combine(KatalogRepo,"logs"));PlikLog=Path.Combine(KatalogRepo,"logs","launcher-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".log");}
    public string KatalogRelease {get{return Path.Combine(KatalogRepo,"release");}}
    public string PlikExe {get{return Path.Combine(KatalogRelease,"MapaDyskow.exe");}}
    public void ZapiszLog(string tresc){File.AppendAllText(PlikLog,DateTime.UtcNow.ToString("o")+" "+tresc+Environment.NewLine,Encoding.UTF8);}
    FileStream Zablokuj(){Directory.CreateDirectory(Path.Combine(KatalogRepo,"build"));return new FileStream(Path.Combine(KatalogRepo,"build","launcher.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    public static string SkrotPliku(string plik){using(var hash=SHA256.Create())using(var dane=File.OpenRead(plik))return BitConverter.ToString(hash.ComputeHash(dane)).Replace("-","").ToLowerInvariant();}
    static string[] Zrodla(string katalog){return Directory.GetFiles(katalog).Where(p=>new[]{".cs",".xaml",".manifest",".config"}.Contains(Path.GetExtension(p).ToLowerInvariant())||Path.GetFileName(p).Equals("buduj.ps1",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(p).Equals("konfiguracja.json",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).ToArray();}
    public static string ObliczSkrotZrodel(string katalog){string tresc=String.Join("\n",Zrodla(katalog).Select(p=>Path.GetFileName(p)+"\n"+SkrotPliku(p)));using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(tresc))).Replace("-","").ToLowerInvariant();}
    public UstawieniaLaunchera WczytajUstawienia(){string plik=Path.Combine(KatalogRepo,"launcher","ustawienia.json");try{return File.Exists(plik)?serializator.Deserialize<UstawieniaLaunchera>(File.ReadAllText(plik,Encoding.UTF8)):new UstawieniaLaunchera();}catch(Exception blad){ZapiszLog("Ustawienia: "+blad.Message);return new UstawieniaLaunchera();}}
    public void ZapiszUstawienia(UstawieniaLaunchera ustawienia){Directory.CreateDirectory(Path.Combine(KatalogRepo,"launcher"));ZapiszAtomowo(Path.Combine(KatalogRepo,"launcher","ustawienia.json"),serializator.Serialize(ustawienia));}
    static void ZapiszAtomowo(string plik,string tresc){string tymczasowy=plik+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(tymczasowy,tresc,Encoding.UTF8);if(File.Exists(plik))File.Replace(tymczasowy,plik,null);else File.Move(tymczasowy,plik);}
    public StanWydania OdczytajStan(){string plik=Path.Combine(KatalogRelease,"stan-wydania.json");return File.Exists(plik)?serializator.Deserialize<StanWydania>(File.ReadAllText(plik,Encoding.UTF8)):null;}
    public bool PoprawneWydanie(){try{var stan=OdczytajStan();return stan!=null&&File.Exists(PlikExe)&&File.Exists(Path.Combine(KatalogRelease,"konfiguracja.json"))&&File.Exists(Path.Combine(KatalogRelease,"MapaDyskow.exe.config"))&&SkrotPliku(PlikExe)==stan.exeSha256;}catch{return false;}}
    bool AplikacjaDziala(){if(!File.Exists(PlikExe))return false;bool nieznany=false;foreach(var proces in Process.GetProcessesByName("MapaDyskow"))using(proces){try{if(Path.GetFullPath(proces.MainModule.FileName).Equals(PlikExe,StringComparison.OrdinalIgnoreCase))return true;}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){nieznany=true;}}
        if(nieznany){try{using(var uchwyt=new FileStream(PlikExe,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){} }catch(IOException){return true;}catch(UnauthorizedAccessException){return true;}}return false;}
    public async Task<WynikAktualizacji> Przygotuj(bool automatycznie,Action<string> postep){
        try{using(var blokada=Zablokuj()){
            if(AplikacjaDziala()){ZapiszLog("Aplikacja działa. Bez budowania i podmiany.");return new WynikAktualizacji {Powod="uruchomiona"};}
            OdzyskajTransakcje();
            string katalogZrodel=Path.Combine(KatalogRepo,"src");string skrotZrodel=await Task.Run(()=>ObliczSkrotZrodel(katalogZrodel)).ConfigureAwait(false);
            bool poprawne=PoprawneWydanie();var stan=poprawne?OdczytajStan():null;
            if(poprawne&&stan.sourceHash==skrotZrodel){ZapiszLog("Bez zmian. Zero builda.");return new WynikAktualizacji {Powod="bez-zmian"};}
            if(!automatycznie){if(!poprawne)throw new InvalidOperationException("Automatyczne budowanie jest wyłączone, a brak poprawnego Release.");ZapiszLog("Automatyczne budowanie wyłączone.");return new WynikAktualizacji {Powod="auto-wylaczone"};}
            postep("Budowanie najnowszej wersji…");string katalogProby=Path.Combine(KatalogRepo,"build","pending",Guid.NewGuid().ToString("N"));string migawka=Path.Combine(katalogProby,"zrodla"),wynik=Path.Combine(katalogProby,"aplikacja");Directory.CreateDirectory(migawka);
            foreach(string plik in Zrodla(katalogZrodel))File.Copy(plik,Path.Combine(migawka,Path.GetFileName(plik)));
            if(ObliczSkrotZrodel(migawka)!=skrotZrodel)throw new IOException("Źródła zmieniły się podczas przygotowania kompilacji. Spróbuj ponownie.");
            string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
            await UruchomProces(powershell,"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(migawka,"buduj.ps1")+"\" -KatalogDocelowy \""+wynik+"\" -Tryb Release",migawka).ConfigureAwait(false);
            string nowyExe=Path.Combine(wynik,"MapaDyskow.exe");foreach(string plik in new[]{"MapaDyskow.exe","MapaDyskow.exe.config","konfiguracja.json","kompilacja.json"})if(!File.Exists(Path.Combine(wynik,plik)))throw new IOException("Niepełny build: "+plik);
            postep("Sprawdzanie nowej wersji…");await UruchomProces(nowyExe,"--test",wynik).ConfigureAwait(false);
            var nowyStan=new StanWydania {version=AssemblyName.GetAssemblyName(nowyExe).Version.ToString(),sourceHash=skrotZrodel,builtAt=DateTime.UtcNow.ToString("o"),exeSha256=SkrotPliku(nowyExe)};
            File.WriteAllText(Path.Combine(wynik,"stan-wydania.json"),serializator.Serialize(nowyStan),Encoding.UTF8);
            if(ObliczSkrotZrodel(katalogZrodel)!=skrotZrodel)throw new IOException("Źródła zmieniły się podczas budowania. Release pozostaje bez zmian.");
            if(AplikacjaDziala()){ZapiszLog("Aplikacja została uruchomiona podczas budowania. Publikacja odłożona.");return new WynikAktualizacji {Powod="uruchomiona"};}
            postep("Publikowanie Release…");Publikuj(wynik);ZapiszLog("Opublikowano "+nowyStan.version+" sourceHash="+skrotZrodel);return new WynikAktualizacji {Zbudowano=true,Powod="opublikowano"};
        }}catch(Exception blad){ZapiszLog(blad.ToString());return new WynikAktualizacji {Blad=blad.Message,Powod="blad"};}
    }
    async Task UruchomProces(string program,string argumenty,string katalog){
        ZapiszLog("Proces: "+program+" "+argumenty);using(var proces=new Process {StartInfo=new ProcessStartInfo(program,argumenty){WorkingDirectory=katalog,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}}){
            if(Path.GetFileName(program).Equals("powershell.exe",StringComparison.OrdinalIgnoreCase))proces.StartInfo.EnvironmentVariables["PSModulePath"]=Path.Combine(Path.GetDirectoryName(program),"Modules")+Path.PathSeparator+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsPowerShell","Modules");
            proces.Start();var wyjscie=proces.StandardOutput.ReadToEndAsync();var bledy=proces.StandardError.ReadToEndAsync();
            bool zakonczony=await Task.Run(()=>proces.WaitForExit(120000)).ConfigureAwait(false);if(!zakonczony){proces.Kill();proces.WaitForExit();}
            ZapiszLog(await wyjscie.ConfigureAwait(false));ZapiszLog(await bledy.ConfigureAwait(false));
            if(!zakonczony)throw new TimeoutException("Lokalny build/kontrola przekroczyły 2 minuty.");if(proces.ExitCode!=0)throw new InvalidOperationException("Build/kontrola zakończyły się kodem "+proces.ExitCode+". Szczegóły w logu.");
        }
    }
    void Publikuj(string wynik){
        Directory.CreateDirectory(KatalogRelease);string kopia=Path.Combine(KatalogRelease,"poprzednie",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(kopia);
        var pliki=plikiWydania.Where(p=>p!="konfiguracja.json"||!File.Exists(Path.Combine(KatalogRelease,p))).ToList();
        var transakcja=new TransakcjaWydania {Kopia=kopia,Pliki=pliki,Poprzednie=pliki.Where(p=>File.Exists(Path.Combine(KatalogRelease,p))).ToList()};
        foreach(string plik in transakcja.Poprzednie)File.Copy(Path.Combine(KatalogRelease,plik),Path.Combine(kopia,plik));
        string dziennik=Path.Combine(KatalogRelease,"transakcja.json");ZapiszAtomowo(dziennik,serializator.Serialize(transakcja));
        try{foreach(string plik in pliki){string cel=Path.Combine(KatalogRelease,plik),nowy=Path.Combine(wynik,plik);if(File.Exists(cel))File.Replace(nowy,cel,null);else File.Move(nowy,cel);}
            Directory.CreateDirectory(Path.Combine(KatalogRelease,"dane","miniatury"));if(!PoprawneWydanie())throw new IOException("Weryfikacja opublikowanego Release nie powiodła się.");File.Delete(dziennik);
        }catch{OdzyskajTransakcje();throw;}
    }
    void OdzyskajTransakcje(){string dziennik=Path.Combine(KatalogRelease,"transakcja.json");if(!File.Exists(dziennik))return;
        if(AplikacjaDziala())throw new IOException("Nie można odzyskać Release podczas pracy aplikacji.");var transakcja=serializator.Deserialize<TransakcjaWydania>(File.ReadAllText(dziennik,Encoding.UTF8));
        string kopia=Path.GetFullPath(transakcja.Kopia);if(!kopia.StartsWith(Path.Combine(KatalogRelease,"poprzednie")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||transakcja.Pliki.Any(p=>!plikiWydania.Contains(p))||transakcja.Poprzednie.Any(p=>!transakcja.Pliki.Contains(p)))throw new IOException("Niepoprawny dziennik publikacji.");
        foreach(string plik in transakcja.Pliki){string cel=Path.Combine(KatalogRelease,plik);if(transakcja.Poprzednie.Contains(plik)){string odtworzony=Path.Combine(KatalogRelease,plik+".odzyskiwanie");File.Copy(Path.Combine(kopia,plik),odtworzony,true);if(File.Exists(cel))File.Replace(odtworzony,cel,null);else File.Move(odtworzony,cel);}else if(File.Exists(cel))File.Delete(cel);}
        File.Delete(dziennik);ZapiszLog("Odtworzono poprzednie wydanie po przerwanej publikacji.");
    }
    public void UruchomAplikacje(){using(var blokada=Zablokuj()){if(!AplikacjaDziala())OdzyskajTransakcje();if(!PoprawneWydanie())throw new IOException("Brak poprawnego ostatniego Release.");Process.Start(new ProcessStartInfo(PlikExe){WorkingDirectory=KatalogRelease,UseShellExecute=true});}}
}
}
