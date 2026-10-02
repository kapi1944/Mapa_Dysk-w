using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LauncherMapy {
public static class TestyLaunchera {
    static void Wymagaj(bool warunek,string opis){if(!warunek)throw new InvalidOperationException("TEST LAUNCHERA: "+opis);}
    static Dictionary<string,string> PlikiRelease(string katalog){return Directory.GetFiles(katalog,"*",SearchOption.AllDirectories).ToDictionary(p=>p.Substring(katalog.Length),WydanieLokalne.SkrotPliku);}
    static bool Jednakowe(Dictionary<string,string> przed,Dictionary<string,string> po){return przed.Count==po.Count&&przed.All(p=>po.ContainsKey(p.Key)&&po[p.Key]==p.Value);}
    public static int Uruchom(string repo){
        string katalog=Path.Combine(repo,"build","testy-launchera",Guid.NewGuid().ToString("N")),proba=Path.Combine(katalog,"repo");Directory.CreateDirectory(Path.Combine(proba,"src"));
        var wyniki=new List<string>();var wydanie=new WydanieLokalne(proba);
        try{
            foreach(string plik in Directory.GetFiles(Path.Combine(repo,"src")))File.Copy(plik,Path.Combine(proba,"src",Path.GetFileName(plik)));
            var pierwszy=wydanie.Przygotuj(true,s=>wydanie.ZapiszLog(s)).GetAwaiter().GetResult();Wymagaj(pierwszy.Blad==null&&pierwszy.Zbudowano&&wydanie.PoprawneWydanie(),"Pierwszy Release");
            string baza=Directory.GetFiles(Path.Combine(proba,"build","pending"),"indeks.sqlite",SearchOption.AllDirectories).Single();File.Copy(baza,Path.Combine(wydanie.KatalogRelease,"dane","indeks.sqlite"));
            File.WriteAllText(Path.Combine(wydanie.KatalogRelease,"dane","miniatury","znacznik.txt"),"Sztuczny cache testowy");File.WriteAllText(Path.Combine(wydanie.KatalogRelease,"wyglad.json"),"{}");
            string konfiguracja=Path.Combine(wydanie.KatalogRelease,"konfiguracja.json");File.AppendAllText(konfiguracja,Environment.NewLine);var danePrzed=new[]{"dane\\indeks.sqlite","dane\\miniatury\\znacznik.txt","wyglad.json","konfiguracja.json"}.ToDictionary(p=>p,p=>WydanieLokalne.SkrotPliku(Path.Combine(wydanie.KatalogRelease,p)));
            var przed=PlikiRelease(wydanie.KatalogRelease);int liczbaKompilacji=Directory.GetDirectories(Path.Combine(proba,"build","pending")).Length;
            var bezZmian=wydanie.Przygotuj(true,s=>{throw new Exception("Nie powinno być okna builda.");}).GetAwaiter().GetResult();
            Wymagaj(bezZmian.Blad==null&&!bezZmian.Zbudowano&&bezZmian.Powod=="bez-zmian"&&liczbaKompilacji==Directory.GetDirectories(Path.Combine(proba,"build","pending")).Length&&Jednakowe(przed,PlikiRelease(wydanie.KatalogRelease)),"A: brak zmian = zero builda i zmian Release");wyniki.Add("A: PASS — zero builda");
            string zrodlo=Path.Combine(proba,"src","AssemblyInfo.cs");File.AppendAllText(zrodlo,"\n// Poprawna zmiana tylko w kopii testowej.\n",Encoding.UTF8);string staryHash=wydanie.OdczytajStan().sourceHash;
            var aktualizacja=wydanie.Przygotuj(true,s=>wydanie.ZapiszLog(s)).GetAwaiter().GetResult();Wymagaj(aktualizacja.Blad==null&&aktualizacja.Zbudowano&&wydanie.PoprawneWydanie()&&wydanie.OdczytajStan().sourceHash!=staryHash,"B: nowy Release po zmianie");
            foreach(var para in danePrzed)Wymagaj(WydanieLokalne.SkrotPliku(Path.Combine(wydanie.KatalogRelease,para.Key))==para.Value,"B: zachowanie danych i konfiguracji");
            Wymagaj(Directory.GetFiles(Path.Combine(wydanie.KatalogRelease,"poprzednie"),"MapaDyskow.exe",SearchOption.AllDirectories).Length==1,"Kopia ostatniego Release");wyniki.Add("B: PASS — nowy Release, poprzedni zachowany, dane nietknięte");
            przed=PlikiRelease(wydanie.KatalogRelease);File.AppendAllText(zrodlo,"\nTO JEST CELOWY BLAD KOMPILACJI\n",Encoding.UTF8);var blad=wydanie.Przygotuj(true,s=>wydanie.ZapiszLog(s)).GetAwaiter().GetResult();
            Wymagaj(blad.Blad!=null&&Jednakowe(przed,PlikiRelease(wydanie.KatalogRelease))&&wydanie.PoprawneWydanie(),"C: błąd kompilacji nie zmienia żadnego pliku Release");wyniki.Add("C: PASS — Release w całości nietknięty po błędzie kompilacji");
            var wylaczone=wydanie.Przygotuj(false,s=>{throw new Exception("Autobuild wyłączony.");}).GetAwaiter().GetResult();Wymagaj(wylaczone.Blad==null&&!wylaczone.Zbudowano&&wylaczone.Powod=="auto-wylaczone","Autobuild OFF");wydanie.ZapiszUstawienia(new UstawieniaLaunchera {AutomatycznieBuduj=false});Wymagaj(!wydanie.WczytajUstawienia().AutomatycznieBuduj,"Pamięć ustawienia");wyniki.Add("Ustawienia: PASS");
            using(var proces=Process.Start(new ProcessStartInfo(wydanie.PlikExe){WorkingDirectory=wydanie.KatalogRelease,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){
                try{var zegar=Stopwatch.StartNew();while(zegar.ElapsedMilliseconds<10000){proces.Refresh();if(proces.HasExited)throw new Exception("Testowa aplikacja nie uruchomiła się.");if(proces.MainWindowHandle!=IntPtr.Zero)break;Thread.Sleep(100);}Wymagaj(proces.MainWindowHandle!=IntPtr.Zero,"Okno testowego Release");
                    var dziala=wydanie.Przygotuj(true,s=>{throw new Exception("Nie budujemy uruchomionej aplikacji.");}).GetAwaiter().GetResult();Wymagaj(dziala.Blad==null&&dziala.Powod=="uruchomiona"&&Jednakowe(przed,PlikiRelease(wydanie.KatalogRelease)),"Uruchomiony EXE pozostaje nietknięty");wyniki.Add("Uruchomiona aplikacja: PASS");
                }finally{if(!proces.HasExited){proces.CloseMainWindow();if(!proces.WaitForExit(5000)){proces.Kill();proces.WaitForExit();}}}
            }
            string kopia=Path.Combine(wydanie.KatalogRelease,"poprzednie","test-odzyskiwania");Directory.CreateDirectory(kopia);
            string plikStanu=Path.Combine(wydanie.KatalogRelease,"stan-wydania.json");File.Copy(plikStanu,Path.Combine(kopia,"stan-wydania.json"));File.WriteAllText(plikStanu,"Przerwana publikacja");
            File.WriteAllText(Path.Combine(wydanie.KatalogRelease,"transakcja.json"),new JavaScriptSerializer().Serialize(new TransakcjaWydania {Kopia=kopia,Pliki=new List<string>{"stan-wydania.json"},Poprzednie=new List<string>{"stan-wydania.json"}}));
            var odzyskane=wydanie.Przygotuj(false,s=>{throw new Exception("Odzyskiwanie nie wymaga builda.");}).GetAwaiter().GetResult();
            Wymagaj(odzyskane.Blad==null&&wydanie.PoprawneWydanie()&&!File.Exists(Path.Combine(wydanie.KatalogRelease,"transakcja.json")),"Odzyskanie przerwanej publikacji");
            foreach(var para in danePrzed)Wymagaj(WydanieLokalne.SkrotPliku(Path.Combine(wydanie.KatalogRelease,para.Key))==para.Value,"Odzyskiwanie zachowuje dane");wyniki.Add("Przerwana publikacja: PASS");
            var aplikacja=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            try{
                var interfejs=new OknoLaunchera(aplikacja,wydanie,wydanie.WczytajUstawienia());
                typeof(OknoLaunchera).GetMethod("PokazBlad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(interfejs,new object[]{"Sztuczny błąd testowy"});
                var okno=(Window)typeof(OknoLaunchera).GetField("okno",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(interfejs);okno.UpdateLayout();
                var panel=(StackPanel)okno.Content;var przyciski=panel.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
                Wymagaj(przyciski.Length==3&&przyciski[0].Content.ToString()=="Uruchom ostatnią działającą"&&przyciski[0].IsEnabled&&przyciski[1].Content.ToString()=="Pokaż log"&&przyciski[2].Content.ToString()=="Zamknij","Przyciski okna błędu");
                Wymagaj(panel.Children.OfType<TextBlock>().Any(p=>p.Text=="Nie udało się zbudować najnowszej wersji"),"Komunikat okna błędu");
                Wymagaj(panel.Children.OfType<CheckBox>().Single().IsChecked==false,"GUI odczytuje zapisane ustawienie");
                var obraz=new RenderTargetBitmap((int)panel.ActualWidth,(int)panel.ActualHeight,96,96,PixelFormats.Pbgra32);obraz.Render(panel);var zapis=new PngBitmapEncoder();zapis.Frames.Add(BitmapFrame.Create(obraz));using(var plik=File.Create(Path.Combine(katalog,"okno-bledu.png")))zapis.Save(plik);
                wyniki.Add("GUI: PASS — komunikat, przyciski i ustawienie; zrzut PNG");
            }finally{aplikacja.Shutdown();}
            File.WriteAllText(Path.Combine(katalog,"wynik.json"),new JavaScriptSerializer().Serialize(new {sukces=true,testy=wyniki,katalog=proba,log=wydanie.PlikLog}),Encoding.UTF8);return 0;
        }catch(Exception blad){wydanie.ZapiszLog(blad.ToString());File.WriteAllText(Path.Combine(katalog,"wynik.json"),new JavaScriptSerializer().Serialize(new {sukces=false,blad=blad.ToString(),testy=wyniki,log=wydanie.PlikLog}),Encoding.UTF8);return 1;}
    }
}
}
