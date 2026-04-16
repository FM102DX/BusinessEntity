from authentik.core.models import User

username = "akadmin"
new_password = "Akadmin123!"

user = User.objects.filter(username=username).first()
if user is None:
    print(f"USER_NOT_FOUND:{username}")
else:
    user.set_password(new_password)
    user.is_active = True
    user.save()
    print(f"PASSWORD_RESET:{username}")
