The documentation can be built locally using Sphinx. Install Python 3 (choco install python on Windows),
then install sphinx and the ReadTheDocs theme:

```pip install sphinx sphinx_rtd_theme```

To build the HTML output, run:

```.\make.bat html```



To use the auto reloading server (rundev.bat), install the sphinx-autobuild package:

```pip install sphinx-autobuild```


Alternatively, use Docker.

To build the image (only required the first time or when requirements.txt changes):
```docker-build.bat```

To run a local server:
```docker-run.bat```