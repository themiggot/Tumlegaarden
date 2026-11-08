# Udgivelse til Simply.com

Komplet vejledning til at lægge Tumlegaarden-sitet på webhotellet hos Simply.com og holde det opdateret.
Følg trinene i rækkefølge første gang. Afsnit 8 beskriver opdateringer bagefter.

**Du skal bruge:**
- Adgang til Simply.com's kontrolpanel for `tumlegaarden.dk`.
- Adgang til GitHub-repoet.
- Et FTP-program: [FileZilla](https://filezilla-project.org/) (Windows/Mac/Linux) eller [WinSCP](https://winscp.net/) (Windows).

---

## 1. Tjek at webhotellet kan køre ASP.NET

Sitet er en ASP.NET Core 10-app og kræver Simply's Windows/IIS-hosting med ASP.NET. Et rent PHP/Linux-webhotel kan ikke køre det.

1. Se under **Website → Oversigt** og **Administration**, om der står ASP.NET, .NET eller Windows.
2. Er du i tvivl, så skriv til Simply's support:
   > Understøtter webhotellet for tumlegaarden.dk ASP.NET Core 10 som self-contained app, og skal vi udgive til win-x64 eller win-x86?
3. Notér svaret om **x64 eller x86**. Du skal bruge det i trin 2.

Du kan også bare prøve: trin 2–5 virker uden database og mail, og trin 6 viser med det samme, om serveren kan køre sitet.

---

## 2. Hent den færdige upload-pakke fra GitHub

GitHub bygger pakken automatisk, så du ikke behøver at installere noget.

1. Åbn repoet på GitHub og gå til fanen **Actions**.
2. Vælg **Byg udgivelsespakke** i venstre side.
3. Hvis Simply sagde **x64**: klik på den øverste grønne kørsel.
   Hvis Simply sagde **x86**: klik **Run workflow**, vælg `win-x86`, klik **Run workflow** og vent ca. 2 minutter, til kørslen er grøn. Klik så på den.
4. Nederst under **Artifacts**: download `tumlegaarden-win-x64` (eller `-win-x86`).
5. Pak zip-filen ud i en tom mappe på din computer, fx `Tumlegaarden-upload`.

Mappen indeholder blandt andet `web.config`, `Tumlegaarden.Web.exe`, `appsettings.json` og mappen `wwwroot`.

> Har du .NET 10 SDK installeret, kan du i stedet bygge selv fra repoets rod:
> `dotnet publish src/Tumlegaarden.Web -c Release -r win-x64 --self-contained -o publish`

---

## 3. Forbered Simply.com

### 3a. HTTPS
**Website → HTTPS beskyttelse**: slå SSL-certifikat til for `tumlegaarden.dk` og `www.tumlegaarden.dk`. Sitet kræver HTTPS.

### 3b. MySQL-database
**Databaser → MySQL**: opret en database. Notér:
- Server (værtsnavn)
- Databasenavn
- Brugernavn
- Adgangskode

Tabellerne oprettes automatisk af sitet ved første start.

### 3c. Mailkonto til afsendelse
**E-mail**: opret `noreply@tumlegaarden.dk` med en lang, tilfældig adgangskode.
Find SMTP-serveren under mailkontoens opsætning/loginoplysninger. Sitet er sat op til `smtp.simply.com` på port 587. Står der noget andet, så brug det.

---

## 4. Lav konfigurationsfilen med hemmelighederne

Denne fil indeholder adgangskoder og må **aldrig** lægges på GitHub. Den uploades kun til webhotellet.

Opret en fil med navnet `appsettings.Production.json` i din upload-mappe (samme mappe som `web.config`) med dette indhold, og udfyld felterne:

```json
{
  "Bootstrap": {
    "AdminEmail": "DIT_NAVN@tumlegaarden.dk"
  },
  "ConnectionStrings": {
    "MySql": "Server=MYSQL_SERVER;Port=3306;Database=DATABASENAVN;User Id=BRUGERNAVN;Password=ADGANGSKODE;SslMode=Required;CharSet=utf8mb4"
  },
  "Smtp": {
    "Host": "smtp.simply.com",
    "Port": 587,
    "UserName": "noreply@tumlegaarden.dk",
    "Password": "MAILKONTOENS_ADGANGSKODE",
    "FromAddress": "noreply@tumlegaarden.dk"
  }
}
```

| Felt | Hvad det er |
|---|---|
| `Bootstrap:AdminEmail` | Den første administrator. Får en invitation, når databasen er tom. Skal være en `@tumlegaarden.dk`-adresse, du kan læse. |
| `ConnectionStrings:MySql` | Oplysningerne fra trin 3b. Indeholder adgangskoden et `;`, så kontakt udvikleren. |
| `Smtp:*` | Mailkontoen fra trin 3c. Port 587 bruger STARTTLS, port 465 bruger SSL. |

Brug en teksteditor (Notesblok, VS Code), ikke Word. Gem som UTF-8.

---

## 5. Upload med FTP

### 5a. Forbind
1. I Simply's kontrolpanel: **Website → File Manager → Loginoplysninger**. Notér server, brugernavn og adgangskode.
2. I FileZilla: **Filer → Serverhåndtering → Ny server**.
   - **Protokol:** SFTP, hvis Simply oplyser det. Ellers *FTP* med kryptering **Kræv eksplicit FTP over TLS**. Brug aldrig almindelig ukrypteret FTP.
   - **Vært, Bruger, Adgangskode:** fra Loginoplysninger.
   - Klik **Forbind**. Godkend certifikatet første gang.

### 5b. Ryd op
Gå til `/public_html` i højre side (serveren).
- **Slet** `index.html` (Simply's standardside).
- **Lad** mappen `.well-known` **være**. Den bruges til SSL-certifikatet.

### 5c. Upload
1. I venstre side (din computer): åbn upload-mappen fra trin 2.
2. Markér **alt indholdet** i mappen (Ctrl+A), ikke selve mappen.
3. Træk det over i `/public_html`.
4. Vent til køen nederst er tom og der står 0 fejlede overførsler. Det er omkring 135 MB.

Bagefter skal `/public_html` se sådan ud (uddrag):

```
/public_html
├── .well-known/
├── wwwroot/
├── appsettings.json
├── appsettings.Production.json   ← din fil fra trin 4
├── Tumlegaarden.Web.exe
├── Tumlegaarden.Web.dll
├── web.config
└── … (mange .dll-filer)
```

Filerne `appsettings*.json` og mappen `App_Data` kan ikke hentes udefra. Alle forespørgsler går gennem appen, som kun udleverer `wwwroot`.

---

## 6. Første start

1. Åbn **https://tumlegaarden.dk**. Første besøg kan tage 10–20 sekunder, mens appen starter og opretter tabellerne.
2. Du skal se login-siden med overskriften **TUMLEGAARDEN** og cookie-meddelelsen nederst.
3. Tjek indbakken for `Bootstrap:AdminEmail`. Der ligger en mail: *"Tumlegaarden: Du er inviteret som administrator"*. Tjek også spam.
4. Åbn linket, vælg adgangskode (mindst 14 tegn, store og små bogstaver, tal og specialtegn).
5. Scan QR-koden med en authenticator-app og indtast koden.
6. **Gem de 10 gendannelseskoder** et sikkert sted, fx i en adgangskodemanager.
7. Gå til **Brugere** og invitér resten af holdet.

Ser du noget andet end login-siden, så gå til afsnit 9.

---

## 7. Kontroller at alt er i orden

- [ ] `http://tumlegaarden.dk` sender videre til `https://`.
- [ ] Login-siden vises, og cookie-meddelelsen forsvinder efter **OK**.
- [ ] Administrator-invitationen kom frem, og to-faktor virker.
- [ ] En invitation til en kollega kommer frem.
- [ ] En `@gmail.com`-adresse afvises ved invitation.
- [ ] [securityheaders.com](https://securityheaders.com/?q=tumlegaarden.dk) giver karakter A eller A+.
- [ ] Åbn `https://tumlegaarden.dk/appsettings.Production.json`. Du skal **ikke** kunne se filen, kun blive sendt til login.

---

## 8. Opdatering af sitet

Når der er ændret kode på GitHub:

1. Hent den nye pakke som i trin 2. Pak den ud i en **ny, tom** mappe.
2. **Upload først `app_offline.htm`** fra repoets mappe `deploy/` til `/public_html`.
   Appen stopper, filerne låses op, og besøgende ser en "opdaterer"-side.
3. Upload indholdet af den nye mappe til `/public_html` og vælg **Overskriv** ved spørgsmål.
   **Upload ikke** en ny `appsettings.Production.json`. Den på serveren beholdes.
4. **Slet `app_offline.htm`** på serveren. Sitet starter igen ved næste besøg.

Rør aldrig mappen `App_Data` på serveren. Den indeholder nøglerne til login-cookies. Slettes den, bliver alle logget ud.

---

## 9. Fejlsøgning

Slå detaljeret log til, når noget fejler:
1. Opret mappen `logs` i `/public_html`.
2. Ret `stdoutLogEnabled="false"` til `"true"` i `web.config` på serveren.
3. Genindlæs siden og læs den nyeste fil i `logs/`. Se også **Website → Webserver logs**.
4. **Slå det fra igen** bagefter.

| Symptom | Sandsynlig årsag | Løsning |
|---|---|---|
| Du ser indholdet af `web.config`, en mappeoversigt, 403 eller PHP-fejl | Webhotellet kører ikke ASP.NET | Se trin 1. Kontakt Simply. |
| **HTTP 500.19** | IIS forstår ikke en linje i `web.config` | Slet linjen med `removeServerHeader="true"` (behold `<requestFiltering>`), og prøv igen. |
| **HTTP 500.30 / 500.32** | Appen kan ikke starte, ofte x64/x86 | Byg med den anden runtime (trin 2) og upload igen. Ellers læs loggen. |
| **HTTP 500.30** med databasefejl i loggen | Forkert forbindelsesstreng | Tjek server, bruger og adgangskode. Ved SSL-fejl: skift `SslMode=Required` til `SslMode=Preferred`. |
| **HTTP 400 Bad Request – Invalid Hostname** | Sitet tilgås via en adresse, der ikke står i `AllowedHosts` | Tilføj adressen i `appsettings.json`, fx `"tumlegaarden.dk;www.tumlegaarden.dk;ny.adresse.dk"`. |
| Ingen invitationsmail | SMTP-oplysninger forkerte, eller mailen er i spam | Læs loggen ("Kunne ikke sende"). Tjek trin 3c og 4. |
| Kom aldrig nogen administrator-invitation | `Bootstrap:AdminEmail` mangler, eller der findes allerede en bruger | Sendes kun når databasen er tom. Ret filen og genstart ved at uploade `app_offline.htm` og slette den igen. |
| Alle bliver logget ud efter hver opdatering | `App_Data` blev slettet eller overskrevet | Upload ikke `App_Data`, og slet den ikke. |
| "For mange forsøg" | Rate limiting: maks. 30 loginforsøg pr. minut pr. IP | Vent et minut. |
| Brugeren har mistet telefon og gendannelseskoder | | En administrator vælger **Nulstil 2FA** under Brugere. |

---

## 10. Backup og sikkerhed

- **Database:** Tag jævnligt backup af MySQL-databasen via kontrolpanelet. Se også **Website → Genskab data**.
- **`App_Data/keys`:** Tag backup af mappen sammen med databasen.
- **Hemmeligheder:** `appsettings.Production.json` findes kun på serveren og hos dig. Læg den aldrig på GitHub eller i mails. Skift adgangskoderne, hvis den slipper ud.
- **FTP:** Brug altid krypteret forbindelse (SFTP eller FTP over TLS).
- **Administratorer:** Hav altid mindst to administratorer, så en mistet telefon ikke låser alle ude.
