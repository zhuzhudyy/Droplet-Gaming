# SpaceEnvironment texture sources

Downloaded 2026-09-07 from the NASA image archive; source image opened and visually inspected at 2048 x 1024. No existing geographic Earth texture was found in the project: the old FleetEarth shader is an original stylized procedural illustration and is preserved unchanged.

## Earth_BaseColor_2048x1024.jpg

- Title: The Blue Marble: Land Surface, Ocean Color, Sea Ice and Clouds (2002).
- Creator/credit: NASA Goddard Space Flight Center. Land, shallow-water and cloud imagery by Reto Stöckli; ocean colour and compositing enhancements by Robert Simmon. MODIS and supporting Earth-observation datasets.
- Primary downloadable source: https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57735/land_ocean_ice_cloud_2048.jpg
- Primary description and credit: https://science.nasa.gov/earth/earth-observatory/the-blue-marble-true-color-global-imagery-at-1km-resolution/
- Legacy catalogue entry (now redirects as NASA migrates Visible Earth): https://visibleearth.nasa.gov/images/57735/the-blue-marble-land-surface-ocean-color-sea-ice-and-clouds
- Usage guidance: https://www.nasa.gov/nasa-brand-center/images-and-media/ and https://heasarc.gsfc.nasa.gov/docs/www_info/credit/nasa.html
- Use: NASA imagery made available under NASA media-usage guidelines; acknowledge NASA, do not imply NASA endorsement. This atlas contains no NASA logos or identifiable people. No third-party copyright notice appears on the cited NASA catalogue/credit. No exclusive rights are asserted over NASA source imagery.
- Local SHA-256: `fb67ac030214c1891994c8f976e7f6c9cd5b0f21586aba8567250781a4fe708e`.
- Pixel dimensions: 2048 x 1024, RGB JPEG. The original download is retained byte-for-byte, with no generation, resizing, recompression, globe lighting, terminator, atmosphere, or sun-glow processing added.
- Use in scene: one equirectangular shared base-colour map including the source's composited clouds, sampled by every Earth LOD. The map is a historic satellite mosaic, not current weather or an astronomically exact reproduction of the reference scene. The separate shaded-topography/globe-lighting products were not selected.
- Runtime format, mipmaps and residency must be measured after Unity import. JPEG bytes on disk are not runtime texture memory.

The Sun mesh and Earth sphere/UV topology are original procedural meshes authored by `Tools/Blender/SpaceEnvironment/celestial_library.py`. The Sun uses one non-emissive base colour and has no texture or light component. No source reference artwork is embedded in these textures or meshes.
