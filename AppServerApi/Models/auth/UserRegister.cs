using System.ComponentModel.DataAnnotations;

public record UserRegister(
    
    [Required(ErrorMessage = "Le pseudo est obligatoire.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Le pseudo doit contenir entre 3 et 20 caractères alphanumériques.")]
    [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Le pseudo doit contenir uniquement des lettres et des chiffres.")]
    string Username,

    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "L'email doit être une adresse e-mail valide.")]
    string Email,
    
    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(20, MinimumLength = 4, ErrorMessage = "Le mot de passe doit contenir entre 4 et 20 caractères.")]
    [RegularExpression(@"^[a-zA-Z0-9&^!@#]+$", ErrorMessage = "Le mot de passe doit contenir uniquement des lettres, des chiffres et les caractères spéciaux &^!@#.")]
    string Password,

    [Required(ErrorMessage = "L'acceptation des termes est obligatoire.")]
    bool Terms
);
