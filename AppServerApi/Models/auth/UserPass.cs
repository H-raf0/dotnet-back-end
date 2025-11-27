using System.ComponentModel.DataAnnotations;

public class UserPass
{
    [Required(ErrorMessage = "Le pseudo est obligatoire.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Le pseudo doit contenir entre 3 et 20 caractères alphanumériques.")]
    [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Le pseudo doit contenir uniquement des lettres et des chiffres.")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(20, MinimumLength = 4, ErrorMessage = "Le mot de passe doit contenir entre 4 et 20 caractères.")]
    [RegularExpression(@"^[a-zA-Z0-9&^!@#]+$", ErrorMessage = "Le mot de passe doit contenir uniquement des lettres, des chiffres et les caractères spéciaux &^!@#.")]
    public string Password { get; set; }

    public UserPass(string username, string password)
    {
        Username = username;
        Password = password;
    }
}