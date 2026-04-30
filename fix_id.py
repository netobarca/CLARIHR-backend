import re

with open("src/CLARIHR.Api/Controllers/PersonnelFileEmploymentController.cs", "r") as f:
    content = f.read()

content = re.sub(r'^\s*id,\s*$', '                publicId,', content, flags=re.MULTILINE)
content = re.sub(r'personnelFileId = id,', 'personnelFileId = publicId,', content)

with open("src/CLARIHR.Api/Controllers/PersonnelFileEmploymentController.cs", "w") as f:
    f.write(content)
