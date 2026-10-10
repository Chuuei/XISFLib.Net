# Typed metadata for XISF and FITS

The library provides typed definitions for reserved XISF properties, additional application properties, and FITS keywords. You can discover each key through IntelliSense and inspect its identifier, CLR type, unit, `Source`, and `SourceReference`. Units describe the stored value; typed access does not convert physical units.

## Choosing a catalog

| Catalog | Contents | Authority |
| --- | --- | --- |
| `XisfKnownProperties` | 159 reserved property definitions, including September 2026 astrometry | [XISF specification](https://pixinsight.com/doc/docs/XISF-1.0-spec/XISF-1.0-spec.html) |
| `XisfAdditionalProperties` | 90 additional properties found in the examples, plus the library's `FITS:BLANK` storage hint | Observed application metadata or library conventions |
| `FitsKnownKeywords.Standard` | Structural, descriptive, image and table keywords; indexed factories | [NASA standard keyword dictionary](https://heasarc.gsfc.nasa.gov/docs/fcg/standard_dict.html) |
| `FitsKnownKeywords.Common` | Published astronomy conventions such as `EXPTIME`, `RA`, and `DEC` | [NASA common keyword dictionary](https://heasarc.gsfc.nasa.gov/docs/fcg/common_dict.html) |
| `FitsKnownKeywords.Astrophotography` | Acquisition, camera, Bayer, focuser, rotator, and weather metadata | [N.I.N.A. keyword documentation](https://nighttime-imaging.eu/docs/master/site/advanced/file_formats/fits/) and linked release notes |
| `FitsKnownKeywords.Examples` | Additional names and representations observed in the example collection | Observed metadata; meaning can depend on the producing application |
| `FitsKnownKeywords.Wcs` | Image WCS axis, matrix and projection parameter factories, with alternate system codes | [FITS 4.0 standard](https://fits.gsfc.nasa.gov/fits_standard.html) |

`MetadataDefinitionSource` explicitly distinguishes `Specification`, `PublishedConvention`, `ObservedExamples`, and `LibraryConvention`. A published convention is not necessarily a reserved FITS keyword. An observed definition records the representation in the examples; it does not establish a universal meaning or unit.

The inventory scanned all 63 XISF and four FITS examples, including document metadata, image properties and FITS cards carried inside XISF. It found 119 distinct XISF property identifiers and 181 distinct FITS keyword names. All these names have typed definitions. `FitsKnownKeywords.All` contains 268 fixed definitions, including separate representations of ambiguous names; 27 indexed factories in `Standard`/`Common` and 12 WCS factories cover additional names. Each catalog's `All` collection is read-only; indexed keys are constructed on demand.

Both formats allow application-defined metadata, so no finite catalog can enumerate every possible name. String-based access remains available and unknown metadata is preserved. See the repository's `Examples/metadata-inventory.json` for the observed identifiers and storage types.

## Additional XISF properties

```csharp
var image = XisfImageData.FromSamples(new byte[] { 7 }, [1, 1], 1);
image.Properties.Set(XisfKnownProperties.Instrument.ExposureTime, 30f);
image.Properties.Set(XisfAdditionalProperties.Instrument.FrameExposureTime, 30.0);
image.Properties.Set(XisfAdditionalProperties.Observer.Name, "Observer");
image.Properties.Set(XisfAdditionalProperties.Pcl.Noise.Sigmas,
    new double[] { 0.01, 0.02, 0.03 });

if (image.Properties.TryGetValue(XisfAdditionalProperties.Pcl.Noise.Sigmas,
    out double[] sigmas))
    Console.WriteLine(sigmas[0]);
```

The additional groups include `Pcl.Noise`, `Pcl.Signal`, `Pcl.Signature`, legacy `Pcl.AstrometricSolution`, and `PixInsight.ProcessingHistory`. Setters preserve the key's declared storage type and copy arrays. Reserved properties retain their specified shape validation; observed application properties impose no unverified dimensions.

`Instrument:FrameExposureTime` and the reserved `Instrument:ExposureTime` are separate properties with different observed/defined storage types. The API does not alias them. The legacy `PCL:AstrometricSolution:*` properties are available for typed metadata access and preservation; they do not activate the September 2026 native astrometric evaluator. `FitsStorage` contains conversion storage hints, rather than ordinary FITS cards.

## FITS header access

```csharp
var hdu = FitsHdu.CreateImage(
    FitsImage.FromPhysicalSamples(new byte[] { 7 }, [1, 1]), primary: true);
hdu.Header.Set(FitsKnownKeywords.Standard.OBJECT, "M31");
hdu.Header.Set(FitsKnownKeywords.Common.EXPTIME, 30.0, "seconds");
hdu.Header.Set(FitsKnownKeywords.Astrophotography.XBINNING, 2L);
hdu.Header.Set(FitsKnownKeywords.Astrophotography.CCD_TEMP, -10.0);
hdu.Header.Add(FitsKnownKeywords.Standard.HISTORY, "Calibration completed");

double exposure = hdu.Header.GetRequiredValue(FitsKnownKeywords.Common.EXPTIME);
if (hdu.Header.TryGetValue(FitsKnownKeywords.Astrophotography.EGAIN,
    out double electronsPerAdu))
    Console.WriteLine(electronsPerAdu);

var coordinateUnit = FitsKnownKeywords.Wcs.CUNIT(1, alternate: 'A');
var matrixElement = FitsKnownKeywords.Wcs.CD(1, 2, alternate: 'A');
var projectionParameter = FitsKnownKeywords.Wcs.PV(1, 0);
var tableFormat = FitsKnownKeywords.Standard.TFORM(2);
var axisLength = FitsKnownKeywords.Standard.NAXISn(1);
```

FITS keys use `long`, `double`, `bool`, or `string`. Numeric getters accept compatible numeric storage and invariant numeric strings, including `D` exponents for real numbers. Integer getters reject fractional, non-finite, and out-of-range values. Booleans are not interpreted as numbers. No sexagesimal or unit conversion is inferred.

`TryGetValue` returns false for missing, null, or incompatible values. `GetRequiredValue` throws `KeyNotFoundException` for absence and `FormatException` for incompatibility. `GetValueOrDefault` supplies a fallback only for missing/null values; incompatible values still throw. Header lookup uses the first matching card and is case-insensitive.

`Set` replaces matching cards. Use `Add` for repeated `COMMENT` and `HISTORY`; typed `Set` rejects these keys to prevent accidental loss of commentary. `Find` returns every matching card in header order and `Remove` removes all matches. Existing structural guards still prevent edits that disagree with image storage.

WCS factories validate axes from 1 through 99, projection parameters from 0 through 99, and optional alternate codes from `A` through `Z`. These factories construct metadata keys; they do not extend the coordinate transformation engine or validate a complete WCS solution. Other indexed factories validate positive indices and the eight-character keyword limit.

## Ambiguous values and dates

Choose the definition matching the producer and representation:

- `Common.APERTURE` is a string aperture name in NASA's dictionary. `Examples.APERTURE` is numeric in this collection; its physical unit is deliberately unspecified.
- `Common.RA`/`DEC` read numeric coordinates. `Common.RAText`/`DECText` preserve textual coordinates such as sexagesimal values. A string getter does not stringify numeric storage.
- `Standard.TNULL(index)` represents a binary-table integer null marker; `TNULLText(index)` represents an ASCII-table string marker.
- `Astrophotography.GAIN` is the acquisition gain setting. `EGAIN` has the documented unit electrons/ADU. These names are not interchangeable.
- Dates such as `DATE-OBS` remain strings. This preserves date-only values, legacy representations, time-system context and precision without assuming an offset. Use the existing astronomy metadata view when its normalized timestamp interpretation fits your application.

The same FITS definitions can create cards embedded in XISF:

```csharp
image.FitsKeywords.Add(XisfFitsKeyword.FromValue(
    FitsKnownKeywords.Standard.OBJECT, "M31"));
image.FitsKeywords.Add(XisfFitsKeyword.FromValue(
    FitsKnownKeywords.Standard.HISTORY, "Calibration completed"));
```

`FromValue` quotes and escapes strings for XISF's textual FITS representation and stores commentary in the appropriate field. Native FITS headers accept CLR values directly.
