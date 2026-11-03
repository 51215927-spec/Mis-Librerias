# CsvManager — Documentación completa

Guía de `CsvManager`, la clase interna que guarda y lee tablas en archivos CSV.

> **Estado de verificación:** el código se compiló con .NET SDK 8.0.132 en Linux, sin advertencias. Se probó guardar, recargar, texto con caracteres especiales, detección de cambios externos y cuatro procesos escribiendo el mismo archivo (sección 7). No se probó en Windows, con archivos grandes ni sobre carpetas de red. Los casos no mencionados en la sección 7 se mantienen como estaban descritos antes y no se volvieron a ejecutar.

# Índice

1. [Qué cambió](#1-qué-cambió)
2. [Cómo se usa ahora](#2-cómo-se-usa-ahora)
3. [El formato del archivo CSV](#3-el-formato-del-archivo-csv)
4. [Errores](#4-errores)
5. [Funcionamiento interno](#5-funcionamiento-interno)
6. [Decisiones y limitaciones](#6-decisiones-y-limitaciones)
7. [Casos probados](#7-casos-probados)

---

# 1. Qué cambió

| Antes | Ahora |
| --- | --- |
| Clase `public static CsvManager` con `Write`, `Read` y `ReadOrEmpty`. | Clase `internal`. Se usa a través de `Modelo.Cargar`, `CargarTodo`, `Guardar` y `GuardarTodo`. |
| `[Atributos.Columna("x")]` sobre campos. | `.Columna(c => c.Miembro, "x")` en `Configurar` (ver la documentación del Modelo). |
| Solo campos. | Campos **o propiedades**, de cualquier visibilidad. |
| La ruta se pasaba en cada llamada. | `.Archivo("ruta.csv")`, una sola vez por entidad. |
| `Read` lanzaba si faltaba el archivo y `ReadOrEmpty` devolvía lista vacía. | `Cargar<T>(exigirArchivo: false)` devuelve tabla vacía; con `true` lanza `FileNotFoundException`. |
| La clase se validaba en cada llamada. | Se valida una vez en `ConstructorModelo.Construir()`. |
| Columnas heredadas detectadas por atributo. | Se declaran explícitamente en la clase derivada. |

`CsvException`, `CsvFormatException` y `CsvConfiguracionException` siguen siendo públicas y no cambiaron. Se agrega `CsvConcurrenciaException` (sección 4).

El formato del archivo (sección 3), el análisis de líneas y la conversión de valores **no cambiaron**. Cambiaron la escritura (archivo temporal con nombre único, vaciado a disco, comprobación de cambios externos) y el bloqueo (ahora también entre procesos; ver 2.3).

# 2. Cómo se usa ahora

```csharp
Datos.Modelo.CargarTodo();                         // lee todos los CSV
Datos.Modelo.Tabla<Persona>().Add(new Persona(...));
Datos.Modelo.Guardar<Persona>();                   // reescribe el CSV completo de Persona
```

## 2.1 Reglas para la clase

| # | Regla |
| --- | --- |
| 1 | Cada dato guardado se declara con `.Columna(x => x.Miembro)`; el miembro es un campo o una propiedad legible. |
| 2 | Debe existir un constructor con un parámetro por columna, con el mismo nombre (sin distinguir mayúsculas) y el mismo tipo que el miembro. |
| 3 | Los tipos son los soportados: texto, `char`, `bool`, números, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, enums y sus versiones `?`. |
| 4 | Los nombres de columna no pueden estar vacíos ni contener `,` `"` `\` ni saltos de línea, y no pueden repetirse. |

Una propiedad calculada (por ejemplo `public int Doble => id * 2;`) se podría declarar como columna y se escribiría, pero la clase no tendría un constructor compatible a menos que haya un parámetro `doble`. Conviene declarar solo datos reales.

## 2.2 Escritura y lectura

| Aspecto | Comportamiento |
| --- | --- |
| Archivo existente | Se reemplaza por completo con el contenido de la tabla. |
| Carpeta inexistente | Se crea. |
| Tabla vacía | Se escribe solo el encabezado. |
| Valor `null` | Se escribe la palabra `null` sin comillas. |
| Enum sin `[Flags]` con un valor no definido | `ArgumentException` al guardar. |
| Corte durante el guardado | El archivo anterior queda intacto (archivo temporal + reemplazo). Antes del reemplazo se fuerza la escritura a disco. |
| Archivo modificado por otro proceso (o a mano) después de cargar | `CsvConcurrenciaException` al guardar. Hay que volver a cargar la tabla. |
| Ruta relativa | Se convierte a absoluta al llamar a `.Archivo(...)`, contra el directorio de trabajo de ese momento. |
| Codificación | UTF-8 sin BOM. |
| Archivo vacío o solo encabezado | Tabla vacía. |
| Orden de las columnas al leer | No importa: se asocian por nombre. |
| Líneas en blanco | Se ignoran. |

`Guardar` escribe **toda** la tabla cada vez.

## 2.3 Varios procesos sobre el mismo archivo

- Cada archivo `datos.csv` tiene un archivo de bloqueo `datos.csv.lock` en su carpeta. Se crea solo y no se borra; se puede ignorar (por ejemplo, en el control de versiones).
- `Leer` y `Escribir` toman ese bloqueo. Si otro proceso lo tiene, esperan hasta 10 segundos y luego lanzan `IOException`.
- `CargarTodo` y `GuardarTodo` (en el `Modelo`) toman los bloqueos de **todos** los archivos juntos, para que otro proceso no lea un conjunto a medio guardar.
- Al cargar o guardar, la tabla recuerda la fecha de modificación y el tamaño del archivo. Si al guardar el archivo es distinto, se lanza `CsvConcurrenciaException` y no se escribe nada.
- Quien reciba `CsvConcurrenciaException` debe volver a cargar y repetir sus cambios. Un patrón posible:

```csharp
while (true)
{
    Datos.Modelo.Cargar<Persona>();
    Datos.Modelo.Tabla<Persona>().Add(new Persona(...));

    try { Datos.Modelo.Guardar<Persona>(); break; }
    catch (CsvConcurrenciaException) { /* otro proceso guardó antes: se reintenta */ }
}
```

- Para que dos programas compartan un archivo deben indicar la **misma ruta absoluta**. Una ruta relativa se resuelve contra el directorio de trabajo de cada programa.

# 3. El formato del archivo CSV

`CsvManager` usa un formato propio, **inspirado en CSV pero no idéntico al estándar**. Conviene conocerlo para editar archivos a mano o depurar.

## 3.1 Estructura

```text
id,nombre,email,nacimiento,estado
1,"Ana Pérez","ana@mail.com",1990-05-17,"Activo"
2,"Luis",null,2001-12-03,"Inactivo"
```

- **Línea 1:** encabezado con los nombres de columna definidos con `.Columna(...)`.
- **Líneas siguientes:** un objeto por línea.
- **Separador:** coma `,`.
- **Orden de columnas al leer:** no importa; se asocian por nombre.
- **Líneas en blanco:** se ignoran.

## 3.2 Cómo se escribe cada tipo

| Tipo | Ejemplo en el archivo |
| --- | --- |
| `string` | `"Ana Pérez"` (siempre entre comillas) |
| `char` | `"x"` (entre comillas) |
| `enum` | `"Activo"` (entre comillas, se guarda el **nombre**) |
| `int`, `long`, etc. | `42` |
| `double`, `decimal` | `3.5` (siempre con **punto**) |
| `bool` | `True` o `False` |
| `DateTime` | `2026-10-01T14:30:00.0000000` |
| `DateOnly` | `2026-10-01` |
| `TimeOnly` | `14:30:00.0000000` |
| `TimeSpan` | `1.02:03:04` (días.horas:minutos:segundos) |
| `Guid` | `3f2504e0-4f89-11d3-9a0c-0305e82c3301` |
| `null` | `null` (la palabra, sin comillas) |

## 3.3 `null` frente a texto vacío

| En el archivo | Significa |
| --- | --- |
| `null` (sin comillas) | `null` |
| `""` (comillas vacías) | Texto vacío `""` |
| `"null"` (con comillas) | El texto `null` |
| Celda vacía sin comillas (`,,`) | `null` (solo al leer; `Escribir` nunca la produce) |

Reglas:

- La palabra `null` se reconoce solo en **minúscula** y **sin comillas**. `Null` o `NULL` sin comillas no son `null`: se intentan convertir al tipo del campo y fallan (o, en un `string`, se leen como texto).
- Esta diferencia solo funciona si el archivo **conserva las comillas**. Si una herramienta las quita, `""` se leerá como `null` y `"null"` también.
- La celda vacía sin comillas se sigue aceptando como `null` para que los archivos escritos con versiones anteriores se lean sin cambios. Al volver a guardarlos quedan con la palabra `null`.

## 3.4 Caracteres especiales dentro de textos

| Carácter | Se escribe |
| --- | --- |
| `\` | `\\` |
| `"` | `\"` |
| Salto de línea | `\n` |
| Retorno de carro | `\r` |
| Tabulación | `\t` |

Ejemplo: el texto `Dijo "hola"` seguido de un salto de línea se guarda como:

```text
"Dijo \"hola\"\n"
```

Así, **cada registro ocupa siempre una sola línea del archivo**, aunque el texto original tenga varios renglones.

## 3.5 Diferencias con el CSV estándar

| Aspecto | CSV estándar (RFC 4180) | CsvManager |
| --- | --- | --- |
| Comilla dentro de un texto | Se duplica: `""` | Se escapa: `\"` |
| Saltos de línea en un texto | Permitidos dentro de comillas | Se escriben como `\n` |
| Separador | Varía según región (`,` o `;`) | Siempre `,` |
| Valor nulo | No existe; suele ser una celda vacía | La palabra `null` sin comillas |

Consecuencia: un archivo escrito por otro programa con comillas duplicadas (`""`) **no se lee correctamente**.

## 3.6 Editar a mano

Reglas para no romper el archivo:

1. No cambiar el encabezado, salvo para reordenar columnas (se puede, siempre que estén todas).
2. Poner los textos entre comillas, sobre todo si contienen comas.
3. Usar punto decimal (`3.5`, no `3,5`).
4. Cada fila debe tener exactamente tantas celdas como el encabezado.
5. Respetar los formatos de fecha de la tabla 3.2.
6. Guardar en UTF-8.
7. Escribir los valores ausentes como `null` (minúscula, sin comillas).
8. Si el programa está abierto, editar el archivo hace que el siguiente `Guardar` falle con `CsvConcurrenciaException` (ver 2.3).

---

# 4. Errores

Jerarquía:

```text
Exception
 └── CsvException
      ├── CsvFormatException          ← el ARCHIVO tiene un problema (Linea, Columna)
      ├── CsvConcurrenciaException    ← otro proceso cambió el archivo desde que se cargó
      └── CsvConfiguracionException   ← la configuración de la entidad está mal
```

Los errores de entrada y salida (`IOException`, `UnauthorizedAccessException`) no se envuelven. Si otro proceso mantiene el bloqueo de un archivo más de 10 segundos, se lanza `IOException`.

## 4.1 `CsvFormatException` — corregir el archivo

Todos comienzan con `Línea N:` o `Línea N, columna 'c':`.

| Mensaje | Causa |
| --- | --- |
| `Parece que el archivo usa ';' como separador...` | Archivo guardado con punto y coma (típico de Excel en español). |
| `Columnas duplicadas en el encabezado: ...` | Una columna aparece dos veces. |
| `Las columnas [...] no existen en X.` | El encabezado tiene nombres que la entidad no define. |
| `Faltan las columnas [...] requeridas por X.` | La entidad tiene columnas que no están en el archivo. |
| `Se esperaban N columnas pero hay M.` | Fila con más o menos celdas que el encabezado. |
| `La línea termina con una barra '\' incompleta.` | Una `\` al final de la línea. |
| `Hay comillas sin cerrar.` | Número impar de comillas. |
| `La celda es null pero X no admite null.` | `null` o celda vacía en un miembro no nulo. |
| `No se pudo convertir 'abc' a Int32: ...` | El texto no corresponde al tipo. |
| `'x' no es un valor válido de Estado. Valores: ...` | Valor de enum inexistente. |
| `El constructor de X rechazó los datos: ...` | El constructor lanzó una excepción propia. |

## 4.2 `CsvConfiguracionException` — corregir el código

Los mensajes de configuración (columnas, tipos, constructor, archivo duplicado) están en la sección 7 de la documentación del Modelo. Se lanzan al llamar a `Construir()`.

## 4.3 Al escribir

| Excepción | Causa |
| --- | --- |
| `ArgumentException`: `La tabla contiene un elemento null.` | Un elemento `null` (`Tabla<T>` ya los rechaza al agregar). |
| `ArgumentException`: `El valor V no está definido en el enum E...` | Enum sin `[Flags]` con un valor fuera de su lista. |
| `CsvConcurrenciaException`: `El archivo ... fue modificado después de cargar X...` | Otro proceso (o una edición a mano) cambió el archivo después de cargar la tabla. Recargar con `Cargar` y repetir los cambios. |

---

# 5. Funcionamiento interno

```text
CsvManager (internal)
├── Bloquear(rutas)                 bloqueo por archivo: Monitor (hilos) + archivo .lock (procesos)
├── Escribir<T>(entidad, datos)     bloqueo; comprueba versión; archivo temporal único; Flush(true); File.Move con reintentos
├── Leer<T>(entidad, exigir)        bloqueo; lee con FileShare.ReadWrite/Delete; ParsearTabla
├── ParsearTabla<T>(entidad, líneas) valida encabezado; BuscarConstructor una vez; una fila = un objeto
├── ValidarConstructor(entidad)     usado por Construir()
├── EsTipoSoportado(tipo)           usado por Construir()
├── BuscarConstructor(tipo, miembros)   empareja parámetros con miembros por nombre y tipo
├── FormatearValor / EscaparString  de valor a texto
└── ParsearLinea / ConvertirValor   de texto a valor
```

Lo que lee y escribe cada columna es un `Miembro` (campo o propiedad obtenido de la lambda): `Miembro.Leer(objeto)` usa `FieldInfo.GetValue` o `PropertyInfo.GetValue`, que funcionan con miembros privados.

La reconstrucción sigue usando el **constructor**, nunca los setters: por eso una propiedad `{ get; }` o `{ get; private set; }` sirve como columna.

El orden de los casos de `FormatearValor`, la lógica de `ParsearLinea` (banderas `escapado`, `dentroDeString`, `huboComillas`) y las reglas de `ConvertirValor` son las mismas que antes.

# 6. Decisiones y limitaciones

| Tema | Detalle |
| --- | --- |
| Bloqueo por archivo | Cada archivo tiene su propio bloqueo, también entre procesos (2.3). Solo protege a programas que usen esta biblioteca: un editor de texto, Excel u otro programa que escriba el CSV sin pasar por ella no respeta el bloqueo (se detecta al guardar, pero no se evita). |
| Carpetas de red | El bloqueo entre procesos depende del sistema de archivos. En recursos de red (SMB, NFS) puede no ser confiable. No se probó. |
| Pérdida de cambios al reintentar | `CsvConcurrenciaException` descarta los cambios en memoria si se recarga. La biblioteca no fusiona cambios: quien guarda decide cómo repetirlos. |
| Archivos grandes | `File.ReadAllLines` carga el archivo entero en memoria. |
| Sin transacciones | `GuardarTodo` bloquea todos los archivos juntos, pero si falla a mitad los ya escritos quedan guardados. |
| Formato propio | No es el CSV estándar de la RFC 4180 (ver 3.5). |
| Cambios de estructura | Renombrar o agregar columnas deja inservibles los archivos viejos. |
| Excel | Puede alterar el archivo al guardarlo (separador, comillas, codificación). Preferir un editor de texto. |
| Datos sensibles | El archivo es texto plano, sin cifrado. |
| Sin API pública | `CsvManager` ya no es público. Si se necesita usarlo sin `Modelo`, hay que volver a exponer `Leer` y `Escribir` y construir un `EntidadModelo` (que también es interno). |

# 7. Casos probados

Con el programa de prueba sobre .NET 8: guardado y recarga de texto con comillas escapadas, enteros, `int?` con valor, `decimal` con punto, propiedades `{ get; }` y `{ get; set; }`, campos privados, herencia (campo privado de la base), archivo inexistente (tabla vacía) y ejecución de `GuardarTodo` con cinco entidades. En la versión con bloqueo entre procesos (Linux, .NET 8): archivo inexistente, texto con comillas, barra, coma y salto de línea, recarga que conserva la misma instancia de tabla, ausencia de archivos `.tmp` sobrantes, `CsvConcurrenciaException` tras una modificación externa, y cuatro procesos agregando 25 filas cada uno sobre el mismo archivo (no se perdió ninguna fila; hubo reintentos por conflicto).

**No probados en esta versión:** Windows (reemplazo de un archivo abierto por otro programa), carpetas de red, `null` escrito y leído en el CSV, columna con nombre distinto al del miembro, fechas, `Guid`, enums `[Flags]`, `char`, archivos con `;`, comillas sin cerrar, línea terminada en `\`, cantidad de columnas incorrecta y excepciones lanzadas por el constructor. Ese código no se modificó respecto de la versión anterior, pero no se volvió a ejecutar.
