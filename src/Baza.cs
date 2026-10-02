using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace MapaDyskow {
public sealed class Baza : IDisposable {
    public static void UtworzPustyIndeks(string sciezka){
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sciezka));
        using(var baza=new Baza(sciezka,true)){
            baza.Wykonaj("BEGIN IMMEDIATE");
            try{
                foreach(string sql in new[]{
                    "CREATE TABLE pliki(id INTEGER PRIMARY KEY,sciezka TEXT UNIQUE,folder TEXT,rozmiar INTEGER,zmiana REAL,typ TEXT,atrybuty INTEGER,urzadzenie TEXT,tozsamosc TEXT,probka TEXT,sha256 TEXT)",
                    "CREATE TABLE zdarzenia(sciezka TEXT,rodzaj TEXT,opis TEXT)",
                    "CREATE TABLE widok_folderow(sciezka TEXT PRIMARY KEY,rodzic TEXT,nazwa TEXT,rozmiar INTEGER,pliki INTEGER,podfoldery INTEGER,zmiana REAL,filmy INTEGER,zdjecia INTEGER,dokumenty INTEGER,archiwa INTEGER,inne INTEGER,duplikaty INTEGER,sygnal INTEGER,systemowy INTEGER,uwagi TEXT,ostatni_skan REAL)",
                    "CREATE TABLE grupy_duplikatow(id INTEGER PRIMARY KEY,sha256 TEXT,rozmiar INTEGER,lokalizacje INTEGER,niezalezne INTEGER,zajete INTEGER,potencjal INTEGER,niepewne INTEGER)",
                    "CREATE TABLE lokalizacje_duplikatow(grupa INTEGER,sciezka TEXT,folder TEXT)",
                    "CREATE TABLE dyski(sciezka TEXT PRIMARY KEY,pojemnosc INTEGER,zajete INTEGER,wolne INTEGER)",
                    "CREATE INDEX rozmiary ON pliki(rozmiar)",
                    "CREATE INDEX probki ON pliki(rozmiar,probka)",
                    "CREATE INDEX hashe ON pliki(rozmiar,sha256)",
                    "CREATE INDEX folder_pliku ON pliki(folder)",
                    "CREATE INDEX foldery_rodzic ON widok_folderow(rodzic,systemowy,rozmiar DESC)",
                    "CREATE INDEX foldery_rozmiar ON widok_folderow(rozmiar DESC)",
                    "CREATE INDEX foldery_filmy ON widok_folderow(filmy DESC)",
                    "CREATE INDEX foldery_zdjecia ON widok_folderow(zdjecia DESC)",
                    "CREATE INDEX foldery_sygnal ON widok_folderow(sygnal,rozmiar DESC)",
                    "CREATE INDEX foldery_zmiana ON widok_folderow(zmiana DESC)",
                    "CREATE INDEX duplikaty_potencjal ON grupy_duplikatow(potencjal DESC)",
                    "CREATE INDEX lokalizacje_grupa ON lokalizacje_duplikatow(grupa)",
                    "CREATE INDEX lokalizacje_sciezka ON lokalizacje_duplikatow(sciezka)"
                })baza.Wykonaj(sql);
                baza.Wykonaj("COMMIT");
            }catch{baza.Wykonaj("ROLLBACK");throw;}
        }
    }
    IntPtr uchwyt;
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] nazwa,out IntPtr baza,int flagi,IntPtr vfs);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close_v2(IntPtr baza);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_busy_timeout(IntPtr baza,int czas);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr baza,byte[] sql,int rozmiar,out IntPtr instrukcja,IntPtr koniec);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_text(IntPtr instrukcja,int indeks,byte[] tekst,int rozmiar,IntPtr destruktor);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_int64(IntPtr instrukcja,int indeks,long liczba);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_double(IntPtr instrukcja,int indeks,double liczba);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_null(IntPtr instrukcja,int indeks);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr instrukcja);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr instrukcja);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr instrukcja);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_type(IntPtr instrukcja,int indeks);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern long sqlite3_column_int64(IntPtr instrukcja,int indeks);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern double sqlite3_column_double(IntPtr instrukcja,int indeks);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr instrukcja,int indeks);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr instrukcja,int indeks);
    [DllImport("winsqlite3.dll", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr baza);
    static byte[] Utf8(string tekst) { return Encoding.UTF8.GetBytes(tekst+"\0"); }
    static string Tekst(IntPtr adres,int rozmiar) { if(adres==IntPtr.Zero)return ""; byte[] bajty=new byte[rozmiar]; Marshal.Copy(adres,bajty,0,rozmiar);return Encoding.UTF8.GetString(bajty); }
    string Blad() { IntPtr adres=sqlite3_errmsg(uchwyt);int rozmiar=0;while(Marshal.ReadByte(adres,rozmiar)!=0)rozmiar++;return Tekst(adres,rozmiar); }
    public Baza(string sciezka,bool zapis) {
        int wynik=sqlite3_open_v2(Utf8(sciezka),out uchwyt,zapis?6:1,IntPtr.Zero);
        if(wynik!=0){ string opis=uchwyt==IntPtr.Zero?"Nie można otworzyć indeksu.":Blad();Dispose();throw new InvalidOperationException(opis); }
        sqlite3_busy_timeout(uchwyt,10000);
        Wykonaj("PRAGMA temp_store=MEMORY");
        if(!zapis)Wykonaj("PRAGMA query_only=ON");
    }
    public List<object[]> Zapytaj(string sql,params object[] argumenty) {
        IntPtr instrukcja=IntPtr.Zero;
        try {
            byte[] tekst=Utf8(sql);int wynik=sqlite3_prepare_v2(uchwyt,tekst,tekst.Length,out instrukcja,IntPtr.Zero);
            if(wynik!=0)throw new InvalidOperationException(Blad());
            for(int i=0;i<argumenty.Length;i++) {
                object wartosc=argumenty[i]; int kod;
                if(wartosc==null)kod=sqlite3_bind_null(instrukcja,i+1);
                else if(wartosc is double || wartosc is float)kod=sqlite3_bind_double(instrukcja,i+1,Convert.ToDouble(wartosc,CultureInfo.InvariantCulture));
                else if(wartosc is long || wartosc is int || wartosc is bool)kod=sqlite3_bind_int64(instrukcja,i+1,Convert.ToInt64(wartosc));
                else { byte[] bajty=Utf8(Convert.ToString(wartosc,CultureInfo.InvariantCulture));kod=sqlite3_bind_text(instrukcja,i+1,bajty,bajty.Length-1,new IntPtr(-1)); }
                if(kod!=0)throw new InvalidOperationException(Blad());
            }
            List<object[]> wiersze=new List<object[]>();
            while((wynik=sqlite3_step(instrukcja))==100) {
                object[] wiersz=new object[sqlite3_column_count(instrukcja)];
                for(int i=0;i<wiersz.Length;i++) {
                    int typ=sqlite3_column_type(instrukcja,i);
                    wiersz[i]=typ==5?null:typ==1?(object)sqlite3_column_int64(instrukcja,i):typ==2?(object)sqlite3_column_double(instrukcja,i):Tekst(sqlite3_column_text(instrukcja,i),sqlite3_column_bytes(instrukcja,i));
                }
                wiersze.Add(wiersz);
            }
            if(wynik!=101)throw new InvalidOperationException(Blad());return wiersze;
        } finally { if(instrukcja!=IntPtr.Zero)sqlite3_finalize(instrukcja); }
    }
    public void Wykonaj(string sql,params object[] argumenty){ Zapytaj(sql,argumenty); }
    public object Wartosc(string sql,params object[] argumenty){var wynik=Zapytaj(sql,argumenty);return wynik.Count==0?null:wynik[0][0];}
    public void Dispose(){if(uchwyt!=IntPtr.Zero){sqlite3_close_v2(uchwyt);uchwyt=IntPtr.Zero;}}
}
}
