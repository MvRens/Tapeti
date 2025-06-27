import os
from packaging.version import Version

def build_releasenotes_index(app):
    releasenotes_path = os.path.join(app.srcdir, 'releasenotes')
    output_file = os.path.join(app.srcdir, 'releasenotes.rst')

    with open(output_file, 'w') as f:
        f.write("Release notes\n")
        f.write("=============\n\n")

        is_first = True

        files = os.listdir(releasenotes_path)
        rst_files = filter(lambda f: f.endswith('.rst'), files)
        versions = list(map(lambda f: os.path.splitext(f)[0], rst_files))
        versions.sort(key=Version, reverse=True)

        for version in versions:

                if not is_first:
                    f.write("----\n\n")
                else:
                    is_first = False

                f.write(f"{version}\n")
                f.write("-" * len(version))
                f.write(f"\n.. include:: releasenotes/{version}.rst\n\n")
